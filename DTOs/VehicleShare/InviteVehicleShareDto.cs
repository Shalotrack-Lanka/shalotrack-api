using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.VehicleShare;

public class InviteVehicleShareDto
{
    [Required]
    public Guid VehicleId { get; set; }

    [Required]
    public string PhoneNumber { get; set; } = string.Empty;
}