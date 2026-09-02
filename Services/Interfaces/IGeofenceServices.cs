using ShaloTrack_API.DTOs.Geofence;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IGeofenceService
{
    Task<ApiResponse<IReadOnlyList<GeofenceResponseDto>>> GetMineAsync();
    Task<ApiResponse<GeofenceResponseDto>> CreateAsync(CreateGeofenceDto dto);
    Task<ApiResponse<GeofenceResponseDto>> UpdateAsync(Guid geofenceId, UpdateGeofenceDto dto);
    Task<ApiResponse<string>> DeleteAsync(Guid geofenceId);
}