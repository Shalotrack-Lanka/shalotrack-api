using ShaloTrack_API.DTOs.DeviceAssignment;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IDeviceAssignmentService
{
    Task<ApiResponse<IReadOnlyList<DeviceAssignmentResponseDto>>> GetAllAsync();

    Task<ApiResponse<DeviceAssignmentResponseDto>> GetByIdAsync(Guid assignmentId);

    Task<ApiResponse<IReadOnlyList<DeviceAssignmentResponseDto>>> GetHistoryByVehicleAsync(Guid vehicleId);

    Task<ApiResponse<IReadOnlyList<DeviceAssignmentResponseDto>>> GetHistoryByDeviceAsync(Guid deviceId);

    Task<ApiResponse<DeviceAssignmentResponseDto>> AssignAsync(CreateDeviceAssignmentDto dto);

    Task<ApiResponse<string>> UnassignAsync(Guid assignmentId);
}