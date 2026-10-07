namespace ShaloTrack_API.Services.Interfaces;

public sealed record AccountPurgeRunResult(int Due, int Purged, int Incomplete, int Failed, bool DryRun);

public interface IAccountPurgeService
{
    /// <summary>
    /// Permanently erases accounts whose 30-day window has passed. With dryRun it only counts and
    /// logs what it WOULD erase; nothing is changed (no Firebase call, no database write).
    /// </summary>
    Task<AccountPurgeRunResult> RunAsync(DateTime nowUtc, bool dryRun, int batchSize, CancellationToken cancellationToken = default);
}