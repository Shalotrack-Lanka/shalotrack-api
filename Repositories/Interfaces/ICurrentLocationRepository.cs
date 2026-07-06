using ShaloTrack_API.DTOs.CurrentLocation;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface ICurrentLocationRepository
{
    Task<List<CurrentLocationResponseDto>> GetAllAsync();
    Task<CurrentLocationResponseDto?> GetByVehicleAsync(Guid vehicleId);
    Task<CurrentLocationResponseDto?> GetByDeviceAsync(Guid deviceId);
}