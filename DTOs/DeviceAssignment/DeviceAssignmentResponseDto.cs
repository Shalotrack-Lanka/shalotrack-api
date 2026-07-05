using ShaloTrack_API.Enums;

namespace ShaloTrack_API.DTOs.DeviceAssignment;

public class DeviceAssignmentResponseDto
{
    public Guid AssignmentId { get; set; }

    public Guid VehicleId { get; set; }

    public string VehicleNumber { get; set; } = string.Empty;

    public Guid DeviceId { get; set; }

    public string ImeiNumber { get; set; } = string.Empty;

    public DateTime AssignedAt { get; set; }

    public DateTime? RemovedAt { get; set; }

    public AssignmentStatus Status { get; set; }
}