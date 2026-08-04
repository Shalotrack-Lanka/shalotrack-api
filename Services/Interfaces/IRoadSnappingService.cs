using ShaloTrack_API.DTOs.Vehicle;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IRoadSnappingService
{
    /// <summary>
    /// Snaps a batch of raw GPS points onto the actual road network using
    /// Google's Roads API, enforcing that the caller owns vehicleId.
    /// Max 100 points per call (Google's own limit).
    /// </summary>
    Task<ApiResponse<IReadOnlyList<SnappedPointDto>>> SnapToRoadAsync(
        Guid vehicleId,
        SnapToRoadRequestDto request);
}