using ShaloTrack_API.DTOs.DeviceStatus;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IDeviceStatusService
{
    Task<ApiResponse<IReadOnlyList<DeviceStatusResponseDto>>> GetAllAsync();

    Task<ApiResponse<DeviceStatusResponseDto>> GetByDeviceAsync(Guid deviceId);

    Task<ApiResponse<DeviceStatusResponseDto>> GetByVehicleAsync(Guid vehicleId);
}