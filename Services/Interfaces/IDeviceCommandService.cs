using ShaloTrack_API.DTOs.Command;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IDeviceCommandService
{
    Task<ApiResponse<DeviceCommandResponseDto>> SendCommandAsync(
        Guid vehicleId,
        SendDeviceCommandDto dto,
        string? firebaseUid,
        bool isStaff);

    Task<ApiResponse<bool>> IsDeviceOnlineAsync(string imei);

    Task<ApiResponse<GatewayDevicesResponseDto>> GetConnectedDevicesAsync();
}