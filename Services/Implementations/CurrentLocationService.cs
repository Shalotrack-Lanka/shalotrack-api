using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.CurrentLocation;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class CurrentLocationService : ICurrentLocationService
{
    private readonly ICurrentLocationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;      // NEW — to resolve owner
    private readonly ICurrentUser _currentUser;    // NEW

    public CurrentLocationService(
        ICurrentLocationRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<CurrentLocationResponseDto>>> GetAllAsync()
    {
        // Staff only — an app user must never pull every vehicle's position.
        if (!_currentUser.IsStaff)
        {
            return ApiResponse<IReadOnlyList<CurrentLocationResponseDto>>.Fail(
                (int)HttpStatusCode.Forbidden, "Forbidden.",
                "This endpoint is restricted to staff.");
        }

        var locations = await _repository.GetAllAsync();
        return ApiResponse<IReadOnlyList<CurrentLocationResponseDto>>.Ok(
            locations, "Current locations retrieved successfully.");
    }

    public async Task<ApiResponse<CurrentLocationResponseDto>> GetByVehicleAsync(Guid vehicleId)
    {
        var location = await _repository.GetByVehicleAsync(vehicleId);
        if (location is null || !await OwnsVehicleAsync(vehicleId))
        {
            return NotFound();
        }
        return ApiResponse<CurrentLocationResponseDto>.Ok(location, "Current location retrieved successfully.");
    }

    public async Task<ApiResponse<CurrentLocationResponseDto>> GetByDeviceAsync(Guid deviceId)
    {
        var location = await _repository.GetByDeviceAsync(deviceId);
        // The DTO carries VehicleId — resolve ownership through it.
        if (location is null || !await OwnsVehicleAsync(location.VehicleId))
        {
            return NotFound();
        }
        return ApiResponse<CurrentLocationResponseDto>.Ok(location, "Current location retrieved successfully.");
    }

    private async Task<bool> OwnsVehicleAsync(Guid vehicleId)
    {
        if (_currentUser.IsStaff) return true;
        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);
        return vehicle is not null &&
               string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal);
    }

    private static ApiResponse<CurrentLocationResponseDto> NotFound() =>
        ApiResponse<CurrentLocationResponseDto>.Fail(
            (int)HttpStatusCode.NotFound, "Current location not found.",
            "No current location exists for the specified resource.");
}