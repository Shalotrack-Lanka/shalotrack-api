using ShaloTrack_API.DTOs.VehicleStats;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IVehicleStatsService
{
    /// <summary>period: "today" | "week" | "month" | "all". Defaults to "today" if null/unrecognized.</summary>
    Task<ApiResponse<VehicleStatsResponseDto>> GetStatsAsync(Guid vehicleId, string? period);
}