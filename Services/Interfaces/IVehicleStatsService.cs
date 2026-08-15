using ShaloTrack_API.DTOs.VehicleStats;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IVehicleStatsService
{
    Task<ApiResponse<VehicleStatsResponseDto>> GetStatsAsync(Guid vehicleId);
}