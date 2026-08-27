using System.Diagnostics;

namespace UserService.Api.Services;

public class CpuSaturationService : BackgroundService
{
    private static readonly TimeSpan BaselineBusyDuration = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan BaselineIdleDuration = TimeSpan.FromMilliseconds(300);

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
                nextSpikeStartsAt = now + spikeInterval;
                isSpiking = false;
            }

            var busy = isSpiking ? SpikeBusyDuration : BaselineBusyDuration;
            var idle = isSpiking ? SpikeIdleDuration : BaselineIdleDuration;

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < busy)
            {
                _ = Math.Sqrt(sw.Elapsed.Ticks);
            }

            try
            {
                Thread.Sleep(idle);
            }
            catch (Exception)
            {
                break;
            }
        }
    }
}
