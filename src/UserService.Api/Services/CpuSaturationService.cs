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
public class CpuSaturationService : BackgroundService
{
    private static readonly TimeSpan BusyDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan IdleDuration = TimeSpan.FromMilliseconds(300);

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

        _logger.LogWarning("CpuSaturationService: starting GameDay Chapter 0 CPU saturation workload");

        var thread = new Thread(() => Spin(stoppingToken))
        {
            IsBackground = true,
            Priority = ThreadPriority.Lowest,
            Name = "gameday-cpu-saturation",
        };
        thread.Start();

        return Task.CompletedTask;
    }

    private static void Spin(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < BusyDuration)
            {
                // 意味のある計算はしない。CPUを使うことそのものが目的。
                _ = Math.Sqrt(sw.Elapsed.Ticks);
            }

            try
            {
                Thread.Sleep(IdleDuration);
            }
            catch (Exception)
            {
                // シャットダウン中などは無視して抜ける
                break;
            }
        }
    }
}
