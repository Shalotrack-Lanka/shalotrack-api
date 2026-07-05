using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.GpsDevice;

public class CreateGpsDeviceDto
{
    [Required]
    [StringLength(15, MinimumLength = 15)]
    public string ImeiNumber { get; set; } = string.Empty;

    public string? SimNumber { get; set; }

    [Required]
    public string DeviceModel { get; set; } = string.Empty;

    [Required]
    public string ProtocolType { get; set; } = string.Empty;

    public string? NetworkProvider { get; set; }

    public string? FirmwareVersion { get; set; }

    public DateTime? WarrantyExpiryDate { get; set; }
}