using ShaloTrack_API.DTOs.VehicleAlertSettings;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IVehicleAlertSettingsService
{
    Task<ApiResponse<VehicleAlertSettingsResponseDto>> GetAsync(Guid vehicleId);
    Task<ApiResponse<VehicleAlertSettingsResponseDto>> UpdateAsync(Guid vehicleId, UpdateVehicleAlertSettingsDto dto);
}