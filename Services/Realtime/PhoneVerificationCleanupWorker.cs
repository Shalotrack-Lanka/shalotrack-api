using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Deletes PhoneVerification rows older than 24 hours, once an hour. No dry-run flag --
/// unlike AccountPurgeWorker this touches no customer-identifying data worth a safety net:
/// a row is just a spent or expired OTP challenge, and every row is already useless after
/// its 5-minute expiry. The 24h grace period (rather than deleting at expiry) is purely so
/// RequestOtpAsync's hourly-per-number cap can still see recent rows while it checks them.
/// </summary>
public class PhoneVerificationCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PhoneVerificationCleanupWorker> _logger;

    public PhoneVerificationCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PhoneVerificationCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ShaloTrackDbContext>();

                var cutoff = DateTime.UtcNow.AddHours(-24);
                var deleted = await db.PhoneVerifications
                    .Where(p => p.CreatedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                {
                    _logger.LogInformation("PhoneVerificationCleanupWorker: deleted {Count} rows older than 24h.", deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PhoneVerificationCleanupWorker: run failed -- will retry in 1 hour.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
