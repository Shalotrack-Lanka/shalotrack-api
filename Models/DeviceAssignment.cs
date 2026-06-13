using ShaloTrack_API.Enums;

namespace ShaloTrack_API.Models;

public class DeviceAssignment
{
    public Guid AssignmentId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? RemovedAt { get; set; }
    public AssignmentStatus Status { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public GpsDevice Device { get; set; } = null!;
}