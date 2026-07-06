using ShaloTrack_API.DTOs.DeviceStatus;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IDeviceStatusRepository
{
    Task<List<DeviceStatusResponseDto>> GetAllAsync();

    Task<DeviceStatusResponseDto?> GetByDeviceAsync(Guid deviceId);

    Task<DeviceStatusResponseDto?> GetByVehicleAsync(Guid vehicleId);
}