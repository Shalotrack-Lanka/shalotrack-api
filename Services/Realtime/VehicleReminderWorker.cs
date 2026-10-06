using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Periodically sends licence / insurance / service reminders (see VehicleReminderNotifier).
/// Real sends only happen between 09:00 and 20:00 Sri Lanka time. Gated by VehicleReminders:DryRun,
/// which defaults to true (fails safe: logs what WOULD be sent, sends and records nothing).
/// </summary>
public class VehicleReminderWorker : BackgroundService
{
    private static readonly TimeSpan SriLankaOffset = TimeSpan.FromMinutes(330);
    private const int FirstSendHour = 9;
    private const int LastSendHour = 20; // exclusive

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VehicleReminderWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly bool _dryRun;

    public VehicleReminderWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<VehicleReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(5, configuration.GetValue("VehicleReminders:IntervalMinutes", 60)));
        _dryRun = configuration.GetValue("VehicleReminders:DryRun", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var timer = new PeriodicTimer(_interval);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VehicleReminderWorker: run failed -- will retry in {Minutes} minutes.", _interval.TotalMinutes);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var localHour = (nowUtc + SriLankaOffset).Hour;

        if (!_dryRun && (localHour < FirstSendHour || localHour >= LastSendHour))
            return; // quiet hours; a notice is only sent for the current day's threshold, so it is picked up next morning

        using var scope = _scopeFactory.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IVehicleReminderNotifier>();
        await notifier.RunAsync(nowUtc, _dryRun, cancellationToken);
    }
}