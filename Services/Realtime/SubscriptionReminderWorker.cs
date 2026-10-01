using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Periodically sends subscription renewal reminders (see SubscriptionReminderService).
/// Real sends only happen between 09:00 and 20:00 Sri Lanka time, so nobody gets a push at 3 a.m.
/// (Sri Lanka has no daylight saving, so a fixed UTC+5:30 offset is exact, and it avoids depending
/// on tzdata inside the container.)
///
/// Gated by SubscriptionReminders:DryRun -- defaults to true (fails safe: logs who WOULD be
/// reminded, sends and records nothing) until explicitly set to false, the same convention as
/// RawPacketRetention:DryRun.
/// </summary>
public class SubscriptionReminderWorker : BackgroundService
{
    private static readonly TimeSpan SriLankaOffset = TimeSpan.FromMinutes(330);
    private const int FirstSendHour = 9;
    private const int LastSendHour = 20; // exclusive

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionReminderWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly bool _dryRun;

    public SubscriptionReminderWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SubscriptionReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(configuration.GetValue("SubscriptionReminders:IntervalMinutes", 30));
        _dryRun = configuration.GetValue("SubscriptionReminders:DryRun", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Let the app finish starting (DB, Firebase) before the first sweep.
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
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
                _logger.LogError(ex, "SubscriptionReminderWorker: run failed -- will retry in {Minutes} minutes.", _interval.TotalMinutes);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var localHour = (nowUtc + SriLankaOffset).Hour;

        if (!_dryRun && (localHour < FirstSendHour || localHour >= LastSendHour))
        {
            return; // quiet hours; the 24-hour reminder window always contains sending hours
        }

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ISubscriptionReminderService>();
        await service.RunAsync(nowUtc, _dryRun, cancellationToken);
    }
}