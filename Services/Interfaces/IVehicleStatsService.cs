using ShaloTrack_API.DTOs.VehicleStats;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IVehicleStatsService
{
    Task<ApiResponse<VehicleStatsResponseDto>> GetStatsAsync(Guid vehicleId, string? period);

    /// <summary>
    /// NEW -- report generation feature. Same stats computation as GetStatsAsync,
    /// but over an explicit [from, to] window instead of a period preset.
    /// </summary>
    Task<ApiResponse<VehicleStatsResponseDto>> GetStatsForRangeAsync(Guid vehicleId, DateTime from, DateTime to);
}