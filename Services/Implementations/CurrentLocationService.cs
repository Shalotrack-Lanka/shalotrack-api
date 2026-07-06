using System.Net;
using ShaloTrack_API.DTOs.CurrentLocation;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class CurrentLocationService : ICurrentLocationService
{
    private readonly ICurrentLocationRepository _repository;

    public CurrentLocationService(
        ICurrentLocationRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<IReadOnlyList<CurrentLocationResponseDto>>> GetAllAsync()
    {
        var locations = await _repository.GetAllAsync();

        return ApiResponse<IReadOnlyList<CurrentLocationResponseDto>>.Ok(
            locations,
            "Current locations retrieved successfully."
        );
    }

    public async Task<ApiResponse<CurrentLocationResponseDto>> GetByVehicleAsync(Guid vehicleId)
    {
        var location = await _repository.GetByVehicleAsync(vehicleId);

        if (location is null)
        {
            return ApiResponse<CurrentLocationResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Current location not found.",
                "No current location exists for the specified vehicle."
            );
        }

        return ApiResponse<CurrentLocationResponseDto>.Ok(
            location,
            "Current location retrieved successfully."
        );
    }

    public async Task<ApiResponse<CurrentLocationResponseDto>> GetByDeviceAsync(Guid deviceId)
    {
        var location = await _repository.GetByDeviceAsync(deviceId);

        if (location is null)
        {
            return ApiResponse<CurrentLocationResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Current location not found.",
                "No current location exists for the specified GPS device."
            );
        }

        return ApiResponse<CurrentLocationResponseDto>.Ok(
            location,
            "Current location retrieved successfully."
        );
    }
}