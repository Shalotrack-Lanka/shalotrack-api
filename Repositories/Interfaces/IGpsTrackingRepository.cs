using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.Filters;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IGpsTrackingRepository
{
    Task<List<GpsTrackingResponseDto>> GetAsync(
        GpsTrackingFilter filter);
}