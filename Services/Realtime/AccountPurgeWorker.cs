using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Hourly run of the account purge (see AccountPurgeService). Gated by AccountDeletion:DryRun, default
/// TRUE (fails safe: logs what it would erase and changes nothing). Set AccountDeletion__DryRun=false
/// only after reading a dry-run log. Settings: AccountDeletion:IntervalMinutes (default 60, min 15),
/// AccountDeletion:BatchSize (customers per run, default 3).
/// </summary>
public class AccountPurgeWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AccountPurgeWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly int _batchSize;
    private readonly bool _dryRun;

    public AccountPurgeWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<AccountPurgeWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(15, configuration.GetValue("AccountDeletion:IntervalMinutes", 60)));
        _batchSize = Math.Clamp(configuration.GetValue("AccountDeletion:BatchSize", 3), 1, 20);
        _dryRun = configuration.GetValue("AccountDeletion:DryRun", true);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AccountPurgeWorker: started (dryRun={DryRun}, every {Minutes} min, batch {Batch}).",
            _dryRun, _interval.TotalMinutes, _batchSize);

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(120), stoppingToken);
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
                using var scope = _scopeFactory.CreateScope();
                var purge = scope.ServiceProvider.GetRequiredService<IAccountPurgeService>();
                var result = await purge.RunAsync(DateTime.UtcNow, _dryRun, _batchSize, stoppingToken);

                if (result.Due > 0)
                {
                    _logger.LogInformation(
                        "AccountPurgeWorker: due={Due} purged={Purged} incomplete={Incomplete} failed={Failed} dryRun={DryRun}",
                        result.Due, result.Purged, result.Incomplete, result.Failed, result.DryRun);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AccountPurgeWorker: run failed -- will retry in {Minutes} minutes.", _interval.TotalMinutes);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}