using System.Net;
using ShaloTrack_API.DTOs.DeviceAssignment;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class DeviceAssignmentService : IDeviceAssignmentService
{
    private readonly IUnitOfWork _unitOfWork;

    public DeviceAssignmentService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<IReadOnlyList<DeviceAssignmentResponseDto>>> GetAllAsync()
    {
        var assignments = await _unitOfWork.DeviceAssignments.GetAllAsync();

        return ApiResponse<IReadOnlyList<DeviceAssignmentResponseDto>>.Ok(
            assignments.Select(ToDto).ToList(),
            "Assignments retrieved successfully."
        );
    }

    public async Task<ApiResponse<DeviceAssignmentResponseDto>> GetByIdAsync(Guid assignmentId)
    {
        var assignment =
            await _unitOfWork.DeviceAssignments.GetByIdAsync(assignmentId);

        if (assignment == null)
        {
            return ApiResponse<DeviceAssignmentResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Assignment not found.",
                "The specified assignment does not exist."
            );
        }

        return ApiResponse<DeviceAssignmentResponseDto>.Ok(
            ToDto(assignment),
            "Assignment retrieved successfully."
        );
    }

    public async Task<ApiResponse<IReadOnlyList<DeviceAssignmentResponseDto>>> GetHistoryByVehicleAsync(Guid vehicleId)
    {
        if (!await _unitOfWork.Vehicles.ExistsAsync(vehicleId))
        {
            return ApiResponse<IReadOnlyList<DeviceAssignmentResponseDto>>.Fail(
                (int)HttpStatusCode.NotFound,
                "Vehicle not found.",
                "The specified vehicle does not exist."
            );
        }

        var assignments =
            await _unitOfWork.DeviceAssignments.GetByVehicleAsync(vehicleId);

        return ApiResponse<IReadOnlyList<DeviceAssignmentResponseDto>>.Ok(
            assignments.Select(ToDto).ToList(),
            "Assignment history retrieved successfully."
        );
    }

    public async Task<ApiResponse<IReadOnlyList<DeviceAssignmentResponseDto>>> GetHistoryByDeviceAsync(Guid deviceId)
    {
        if (!await _unitOfWork.GpsDevices.ExistsAsync(deviceId))
        {
            return ApiResponse<IReadOnlyList<DeviceAssignmentResponseDto>>.Fail(
                (int)HttpStatusCode.NotFound,
                "GPS device not found.",
                "The specified GPS device does not exist."
            );
        }

        var assignments =
            await _unitOfWork.DeviceAssignments.GetByDeviceAsync(deviceId);

        return ApiResponse<IReadOnlyList<DeviceAssignmentResponseDto>>.Ok(
            assignments.Select(ToDto).ToList(),
            "Assignment history retrieved successfully."
        );
    }

    public async Task<ApiResponse<DeviceAssignmentResponseDto>> AssignAsync(CreateDeviceAssignmentDto dto)
    {
        if (!await _unitOfWork.Vehicles.ExistsAsync(dto.VehicleId))
        {
            return ApiResponse<DeviceAssignmentResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Vehicle not found.",
                "The specified vehicle does not exist."
            );
        }

        if (!await _unitOfWork.GpsDevices.ExistsAsync(dto.DeviceId))
        {
            return ApiResponse<DeviceAssignmentResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "GPS device not found.",
                "The specified GPS device does not exist."
            );
        }

        var activeVehicle =
            (await _unitOfWork.DeviceAssignments
                .GetByVehicleAsync(dto.VehicleId, true))
            .FirstOrDefault();

        if (activeVehicle != null)
        {
            return ApiResponse<DeviceAssignmentResponseDto>.Fail(
                (int)HttpStatusCode.Conflict,
                "Vehicle already assigned.",
                "This vehicle already has an active GPS device."
            );
        }

        var activeDevice =
            (await _unitOfWork.DeviceAssignments
                .GetByDeviceAsync(dto.DeviceId, true))
            .FirstOrDefault();

        if (activeDevice != null)
        {
            return ApiResponse<DeviceAssignmentResponseDto>.Fail(
                (int)HttpStatusCode.Conflict,
                "GPS device already assigned.",
                "This GPS device is already assigned to another vehicle."
            );
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync();

            var assignment = new DeviceAssignment
            {
                AssignmentId = Guid.NewGuid(),
                VehicleId = dto.VehicleId,
                DeviceId = dto.DeviceId,
                AssignedAt = DateTime.UtcNow,
                Status = AssignmentStatus.Active
            };

            await _unitOfWork.DeviceAssignments.AddAsync(assignment);

            var device =
                await _unitOfWork.GpsDevices.GetByIdAsync(dto.DeviceId);

            if (device != null)
            {
                device.InstalledAt = DateTime.UtcNow;
                device.UpdatedAt = DateTime.UtcNow;

                _unitOfWork.GpsDevices.Update(device);
            }

            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();

            assignment =
                await _unitOfWork.DeviceAssignments.GetByIdAsync(
                    assignment.AssignmentId)
                ?? assignment;

            return ApiResponse<DeviceAssignmentResponseDto>.Ok(
                ToDto(assignment),
                "GPS device assigned successfully."
            );
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<ApiResponse<string>> UnassignAsync(Guid assignmentId)
    {
        var assignment =
            await _unitOfWork.DeviceAssignments.GetByIdAsync(assignmentId);

        if (assignment == null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound,
                "Assignment not found.",
                "The specified assignment does not exist."
            );
        }

        if (assignment.Status == AssignmentStatus.Removed)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.BadRequest,
                "Already removed.",
                "This assignment has already been removed."
            );
        }

        try
        {
            await _unitOfWork.BeginTransactionAsync();

            assignment.Status = AssignmentStatus.Removed;
            assignment.RemovedAt = DateTime.UtcNow;

            _unitOfWork.DeviceAssignments.Update(assignment);

            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<string>.Ok(
                "Device unassigned successfully.",
                "Device unassigned successfully."
            );
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private static DeviceAssignmentResponseDto ToDto(DeviceAssignment assignment)
    {
        return new DeviceAssignmentResponseDto
        {
            AssignmentId = assignment.AssignmentId,
            VehicleId = assignment.VehicleId,
            VehicleNumber = assignment.Vehicle.VehicleNumber,
            DeviceId = assignment.DeviceId,
            ImeiNumber = assignment.Device.ImeiNumber,
            AssignedAt = assignment.AssignedAt,
            RemovedAt = assignment.RemovedAt,
            Status = assignment.Status
        };
    }
}