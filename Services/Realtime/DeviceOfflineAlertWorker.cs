using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Periodically asks DeviceOfflineNotifier to alert owners about trackers that went silent.
/// Gated by DeviceOffline:DryRun, which defaults to true (fails safe: logs what WOULD be sent, saves and
/// sends nothing). Config (all under "DeviceOffline:"):
///   DryRun             true    log only
///   ThresholdMinutes   15      silent this long = offline (minimum 5)
///   IntervalSeconds    60      sweep period (minimum 30)
///   MaxAgeHours        24      silent longer than this = abandoned, never alerted
///   MaxAlertsPerSweep  100
///   MassOutageMin      20      see DeviceOfflineNotifier
/// </summary>
public class DeviceOfflineAlertWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DeviceOfflineAlertWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly DeviceOfflineOptions _options;
    private readonly bool _dryRun;

    public DeviceOfflineAlertWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DeviceOfflineAlertWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _dryRun = configuration.GetValue("DeviceOffline:DryRun", true);
        _interval = TimeSpan.FromSeconds(Math.Max(30, configuration.GetValue("DeviceOffline:IntervalSeconds", 60)));
        _options = new DeviceOfflineOptions(
            Threshold: TimeSpan.FromMinutes(Math.Max(5, configuration.GetValue("DeviceOffline:ThresholdMinutes", 15))),
            MaxAge: TimeSpan.FromHours(Math.Max(1, configuration.GetValue("DeviceOffline:MaxAgeHours", 24))),
            MaxPerSweep: Math.Max(1, configuration.GetValue("DeviceOffline:MaxAlertsPerSweep", 100)),
            MassOutageMin: Math.Max(1, configuration.GetValue("DeviceOffline:MassOutageMin", 20)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the gateway and the database connections settle after a deploy before the first sweep.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _logger.LogInformation(
            "DeviceOfflineAlertWorker started: threshold {Threshold} min, every {Interval} s, dryRun={DryRun}.",
            _options.Threshold.TotalMinutes, _interval.TotalSeconds, _dryRun);

        using var timer = new PeriodicTimer(_interval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var notifier = scope.ServiceProvider.GetRequiredService<IDeviceOfflineNotifier>();
                await notifier.RunAsync(DateTime.UtcNow, _options, _dryRun, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeviceOfflineAlertWorker: sweep failed -- will retry in {Seconds} s.", _interval.TotalSeconds);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
