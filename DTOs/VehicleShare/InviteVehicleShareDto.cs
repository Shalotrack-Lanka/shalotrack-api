using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.VehicleShare;

public class InviteVehicleShareDto
{
    [Required]
    public Guid VehicleId { get; set; }

    [Required]
    [MaxLength(25)]
    public string PhoneNumber { get; set; } = string.Empty;
}