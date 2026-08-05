namespace ShaloTrack_API.Services.Interfaces;

/// <summary>
/// Phase 3b: orchestrates archive (via ITripArchivalService) + delete for a
/// closed trip, under a Postgres advisory lock scoped per device. Gated by
/// GpsArchive:PurgeDryRun -- defaults to true (fails safe: no deletes unless
/// explicitly turned off in config).
/// </summary>
public interface ITripPurgeService
{
    Task ArchiveAndPurgeTripAsync(
        Guid deviceId,
        Guid vehicleId,
        DateTime tripEndTime,
        CancellationToken cancellationToken = default);
}