namespace ShaloTrack_API.Services.Interfaces;

public record TripArchivalResult(
    bool Success,
    string? S3Key,
    int PointCount,
    string? ErrorMessage);

public interface ITripArchivalService
{
    Task<TripArchivalResult> ArchiveTripAsync(
        Guid deviceId,
        Guid vehicleId,
        DateTime tripEndTime,
        CancellationToken cancellationToken = default);
}