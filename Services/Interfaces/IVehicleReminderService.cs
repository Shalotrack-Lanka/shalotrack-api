using ShaloTrack_API.DTOs.VehicleReminder;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IVehicleReminderService
{
    Task<ApiResponse<IReadOnlyList<VehicleReminderResponseDto>>> GetForVehicleAsync(Guid vehicleId);
    Task<ApiResponse<VehicleReminderResponseDto>> UpsertAsync(Guid vehicleId, UpsertVehicleReminderDto dto);
    Task<ApiResponse<string>> DeleteAsync(Guid reminderId);
}