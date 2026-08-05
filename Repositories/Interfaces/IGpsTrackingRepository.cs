using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.Filters;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IGpsTrackingRepository
{
    Task<List<GpsTrackingResponseDto>> GetAsync(GpsTrackingFilter filter);
    Task<List<TrackingPointRaw>> GetPointsForTripsAsync(Guid vehicleId, DateTime from, DateTime to);

    // NEW -- Phase 3b. Bounded by DeviceId + EventTime range, matching how the
    // archive itself is scoped. Never called without a range -- there is no
    // "delete everything for this device" method here on purpose.
    Task<int> CountByDeviceInRangeAsync(Guid deviceId, DateTime from, DateTime to);
    Task<int> DeleteByDeviceInRangeAsync(Guid deviceId, DateTime from, DateTime to);
}