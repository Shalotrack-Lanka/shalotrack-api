using ShaloTrack_API.Enums;
using System.ComponentModel.DataAnnotations;
namespace ShaloTrack_API.Models;

public class DeviceAssignment
{
    //primary key
    [Key]
    public Guid AssignmentId { get; set; }
    public Guid VehicleId { get; set; }
    public Guid DeviceId { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? RemovedAt { get; set; }
    public AssignmentStatus Status { get; set; }
    public Vehicle Vehicle { get; set; } = null!;
    public GpsDevice Device { get; set; } = null!;
}