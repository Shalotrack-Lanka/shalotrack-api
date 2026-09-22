using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.Filters;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IGpsTrackingRepository
{
    Task<List<GpsTrackingResponseDto>> GetAsync(GpsTrackingFilter filter);

    /// <summary>
    /// Returns raw GPS points ordered by EventTime for trip-detection and playback.
    /// For regular vehicles only points associated with an <em>Active</em> device
    /// assignment are returned so that stale data from a previously linked device
    /// never bleeds through.  For the demo vehicle (<paramref name="isDemoVehicle"/>
    /// = true) the Active constraint is relaxed — the demo must always show data
    /// regardless of the assignment lifecycle.
    /// </summary>
    Task<List<TrackingPointRaw>> GetPointsForTripsAsync(
        Guid vehicleId, DateTime from, DateTime to, bool isDemoVehicle = false);

    // Phase 3b — bounded by DeviceId + EventTime range.
    // There is deliberately no "delete everything for this device" method.
    Task<int> CountByDeviceInRangeAsync(Guid deviceId, DateTime from, DateTime to);
    Task<int> DeleteByDeviceInRangeAsync(Guid deviceId, DateTime from, DateTime to);
}