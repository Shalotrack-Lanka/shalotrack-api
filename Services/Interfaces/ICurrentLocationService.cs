using ShaloTrack_API.DTOs.CurrentLocation;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface ICurrentLocationService
{
    Task<ApiResponse<IReadOnlyList<CurrentLocationResponseDto>>> GetAllAsync();
    Task<ApiResponse<CurrentLocationResponseDto>> GetByVehicleAsync(Guid vehicleId);
    Task<ApiResponse<CurrentLocationResponseDto>> GetByDeviceAsync(Guid deviceId);
}