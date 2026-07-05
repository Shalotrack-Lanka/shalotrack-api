using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.DeviceAssignment;

public class CreateDeviceAssignmentDto
{
    [Required]
    public Guid VehicleId { get; set; }

    [Required]
    public Guid DeviceId { get; set; }
}