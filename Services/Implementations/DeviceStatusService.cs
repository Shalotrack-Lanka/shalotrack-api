using System.Net;
using ShaloTrack_API.DTOs.DeviceStatus;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class DeviceStatusService : IDeviceStatusService
{
    private readonly IDeviceStatusRepository _repository;

    public DeviceStatusService(
        IDeviceStatusRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<IReadOnlyList<DeviceStatusResponseDto>>> GetAllAsync()
    {
        var statuses = await _repository.GetAllAsync();

        return ApiResponse<IReadOnlyList<DeviceStatusResponseDto>>.Ok(
            statuses,
            "Device statuses retrieved successfully."
        );
    }

    public async Task<ApiResponse<DeviceStatusResponseDto>> GetByDeviceAsync(Guid deviceId)
    {
        var status = await _repository.GetByDeviceAsync(deviceId);

        if (status is null)
        {
            return ApiResponse<DeviceStatusResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Device status not found.",
                "No status exists for the specified GPS device."
            );
        }

        return ApiResponse<DeviceStatusResponseDto>.Ok(
            status,
            "Device status retrieved successfully."
        );
    }

    public async Task<ApiResponse<DeviceStatusResponseDto>> GetByVehicleAsync(Guid vehicleId)
    {
        var status = await _repository.GetByVehicleAsync(vehicleId);

        if (status is null)
        {
            return ApiResponse<DeviceStatusResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Device status not found.",
                "No status exists for the specified vehicle."
            );
        }

        return ApiResponse<DeviceStatusResponseDto>.Ok(
            status,
            "Device status retrieved successfully."
        );
    }
}