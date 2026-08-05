namespace ShaloTrack_API.Services.Interfaces;

public record TripArchivalResult(
    bool Success,
    string? S3Key,
    int PointCount,
    string? ErrorMessage,
    DateTime? TripStart = null); // NEW -- Phase 3b needs this to purge the exact same window that got archived, without re-resolving it a second time

public interface ITripArchivalService
{
    Task<TripArchivalResult> ArchiveTripAsync(
        Guid deviceId,
        Guid vehicleId,
        DateTime tripEndTime,
        CancellationToken cancellationToken = default);
}