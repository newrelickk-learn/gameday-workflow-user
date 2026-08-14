using System.Diagnostics;

namespace UserService.Api.Services;

// GameDay第0章用: USER_POD_ROLE=primary のPodだけ、CPUを継続的に使い切る負荷を発生させる。
// New Relicで本物のCPU飽和（k8s.pod.cpu_limit_utilization等）が観測できるようにするための演出。
//
// 安全のため:
// - ワーカーは1スレッドのみ・優先度Lowest（health応答やリクエスト処理を長時間ブロックしないため）
// - タイトスピンにせず、短い稼働と長めの休止を繰り返すduty cycleにする
// - CPU limitを絞ってさえあれば（k8s側で100m程度に設定）、この程度の負荷でも
//   cgroupのCPUクォータはほぼ使い切り、utilizationはほぼ100%に張り付く
// - CPU_SATURATION_ENABLED=false にすれば、Pod再起動（kubectl set env等）だけで即無効化できる
//
// オプション: 周期的なスパイク（既定オフ）
// CPU_SPIKE_INTERVAL_MINUTES を1以上に設定すると、通常のduty cycle（ベースライン）に加えて
// 一定間隔でCPU_SPIKE_DURATION_SECONDS秒だけ、より高いduty cycleに切り替えてスパイクを起こす。
// スパイク時間はliveness/readinessの猶予（periodSeconds×failureThreshold、deployment.yaml側で
// 15秒×8回=120秒）より十分短く保つこと。既定の20秒であれば安全マージンは十分にある。
public class CpuSaturationService : BackgroundService
{
    private static readonly TimeSpan BaselineBusyDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan BaselineIdleDuration = TimeSpan.FromMilliseconds(300);

    // スパイク中のduty cycle。BaselineよりCPUを使う時間の比率を大きくする
    private static readonly TimeSpan SpikeBusyDuration = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SpikeIdleDuration = TimeSpan.FromMilliseconds(30);

    private readonly IConfiguration _configuration;
    private readonly ILogger<CpuSaturationService> _logger;

    public CpuSaturationService(IConfiguration configuration, ILogger<CpuSaturationService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var podRole = _configuration["USER_POD_ROLE"];
        var enabled = _configuration.GetValue("CPU_SATURATION_ENABLED", true);

        if (!enabled || !string.Equals(podRole, "primary", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "CpuSaturationService: skipped (USER_POD_ROLE={PodRole}, CPU_SATURATION_ENABLED={Enabled})",
                podRole,
                enabled);
            return Task.CompletedTask;
        }

        var spikeIntervalMinutes = _configuration.GetValue("CPU_SPIKE_INTERVAL_MINUTES", 0);
        var spikeDurationSeconds = _configuration.GetValue("CPU_SPIKE_DURATION_SECONDS", 20);

        _logger.LogWarning(
            "CpuSaturationService: starting GameDay Chapter 0 CPU saturation workload (spikeIntervalMinutes={SpikeIntervalMinutes}, spikeDurationSeconds={SpikeDurationSeconds})",
            spikeIntervalMinutes,
            spikeDurationSeconds);

        var thread = new Thread(() => Spin(
            stoppingToken,
            spikeIntervalMinutes > 0 ? TimeSpan.FromMinutes(spikeIntervalMinutes) : TimeSpan.Zero,
            TimeSpan.FromSeconds(spikeDurationSeconds)))
        {
            IsBackground = true,
            Priority = ThreadPriority.Lowest,
            Name = "gameday-cpu-saturation",
        };
        thread.Start();

        return Task.CompletedTask;
    }

    private static void Spin(CancellationToken token, TimeSpan spikeInterval, TimeSpan spikeDuration)
    {
        var spikeEnabled = spikeInterval > TimeSpan.Zero;
        var nextSpikeStartsAt = DateTime.UtcNow + spikeInterval;

        while (!token.IsCancellationRequested)
        {
            var now = DateTime.UtcNow;
            var isSpiking = spikeEnabled && now >= nextSpikeStartsAt;

            if (isSpiking && now >= nextSpikeStartsAt + spikeDuration)
            {
                // スパイク期間が終わった。次のスパイク開始時刻を再設定してベースラインに戻る
                nextSpikeStartsAt = now + spikeInterval;
                isSpiking = false;
            }

            var busy = isSpiking ? SpikeBusyDuration : BaselineBusyDuration;
            var idle = isSpiking ? SpikeIdleDuration : BaselineIdleDuration;

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < busy)
            {
                // 意味のある計算はしない。CPUを使うことそのものが目的。
                _ = Math.Sqrt(sw.Elapsed.Ticks);
            }

            try
            {
                Thread.Sleep(idle);
            }
            catch (Exception)
            {
                // シャットダウン中などは無視して抜ける
                break;
            }
        }
    }
}
