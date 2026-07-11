using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.DeviceStatus;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class DeviceStatusService : IDeviceStatusService
{
    private readonly IDeviceStatusRepository _repository;
    private readonly IUnitOfWork _unitOfWork;      // NEW
    private readonly ICurrentUser _currentUser;    // NEW

    public DeviceStatusService(
        IDeviceStatusRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<DeviceStatusResponseDto>>> GetAllAsync()
    {
        if (!_currentUser.IsStaff)
        {
            return ApiResponse<IReadOnlyList<DeviceStatusResponseDto>>.Fail(
                (int)HttpStatusCode.Forbidden, "Forbidden.",
                "This endpoint is restricted to staff.");
        }

        var statuses = await _repository.GetAllAsync();
        return ApiResponse<IReadOnlyList<DeviceStatusResponseDto>>.Ok(
            statuses, "Device statuses retrieved successfully.");
    }

    public async Task<ApiResponse<DeviceStatusResponseDto>> GetByDeviceAsync(Guid deviceId)
    {
        var status = await _repository.GetByDeviceAsync(deviceId);
        // VehicleId is nullable (unassigned device). No vehicle -> no owner -> 404 for non-staff.
        if (status is null || !await OwnsVehicleAsync(status.VehicleId))
        {
            return NotFound();
        }
        return ApiResponse<DeviceStatusResponseDto>.Ok(status, "Device status retrieved successfully.");
    }

    public async Task<ApiResponse<DeviceStatusResponseDto>> GetByVehicleAsync(Guid vehicleId)
    {
        var status = await _repository.GetByVehicleAsync(vehicleId);
        if (status is null || !await OwnsVehicleAsync(vehicleId))
        {
            return NotFound();
        }
        return ApiResponse<DeviceStatusResponseDto>.Ok(status, "Device status retrieved successfully.");
    }

    private async Task<bool> OwnsVehicleAsync(Guid? vehicleId)
    {
        if (_currentUser.IsStaff) return true;
        if (vehicleId is null) return false;
        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId.Value);
        return vehicle is not null &&
               string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal);
    }

    private static ApiResponse<DeviceStatusResponseDto> NotFound() =>
        ApiResponse<DeviceStatusResponseDto>.Fail(
            (int)HttpStatusCode.NotFound, "Device status not found.",
            "No status exists for the specified resource.");
}