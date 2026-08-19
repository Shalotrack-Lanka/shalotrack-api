using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.CurrentLocation;
using ShaloTrack_API.Enums;
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

    // FIX: was owner-or-staff only, which is exactly why a shared viewer
    // got rejected here even after accepting a share -- this endpoint
    // never knew Vehicle Sharing existed. Now also allows access if the
    // requesting customer has an Accepted (not Pending) share for this
    // specific vehicle, matching the "full access" decision already made
    // for what a shared viewer gets.
    private async Task<bool> OwnsVehicleAsync(Guid vehicleId)
    {
        if (_currentUser.IsStaff) return true;

        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);
        if (vehicle is null) return false;

        if (string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal))
        {
            return true;
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(_currentUser.FirebaseUid ?? string.Empty);
        if (customer is null) return false;

        var share = await _unitOfWork.VehicleShares.GetByVehicleAndSharedWithAsync(vehicleId, customer.CustomerId);
        return share is not null && share.Status == VehicleShareStatus.Accepted;
    }

    private static ApiResponse<CurrentLocationResponseDto> NotFound() =>
        ApiResponse<CurrentLocationResponseDto>.Fail(
            (int)HttpStatusCode.NotFound, "Current location not found.",
            "No current location exists for the specified resource.");
}