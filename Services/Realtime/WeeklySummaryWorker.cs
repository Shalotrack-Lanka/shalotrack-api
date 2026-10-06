using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Triggers the weekly summary push (see WeeklySummaryNotifier). It wakes hourly but real sends
/// happen only between 09:00 and 20:00 Sri Lanka time (the notifier restricts the days to
/// Monday, plus Tuesday as catch-up). Gated by WeeklySummary:DryRun, default true (fails safe:
/// logs a tiny sample of what WOULD be sent; sends and records nothing).
/// </summary>
public class WeeklySummaryWorker : BackgroundService
{
    private static readonly TimeSpan SriLankaOffset = TimeSpan.FromMinutes(330);
    private const int FirstSendHour = 9;
    private const int LastSendHour = 20; // exclusive

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WeeklySummaryWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly bool _dryRun;

    public WeeklySummaryWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<WeeklySummaryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(15, configuration.GetValue("WeeklySummary:IntervalMinutes", 60)));
        _dryRun = configuration.GetValue("WeeklySummary:DryRun", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);
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
                _logger.LogError(ex, "WeeklySummaryWorker: run failed -- will retry in {Minutes} minutes.", _interval.TotalMinutes);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var localHour = (nowUtc + SriLankaOffset).Hour;

        // Dry run is cheap (a tiny sample), so it runs at any hour; real sends respect quiet hours.
        if (!_dryRun && (localHour < FirstSendHour || localHour >= LastSendHour))
            return;

        using var scope = _scopeFactory.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IWeeklySummaryNotifier>();
        await notifier.RunAsync(nowUtc, _dryRun, cancellationToken);
    }
}