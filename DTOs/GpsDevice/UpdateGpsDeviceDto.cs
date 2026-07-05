using System.ComponentModel.DataAnnotations;
using ShaloTrack_API.Enums;

namespace ShaloTrack_API.DTOs.GpsDevice;

public class UpdateGpsDeviceDto
{
    public string? SimNumber { get; set; }

    [Required]
    public string DeviceModel { get; set; } = string.Empty;

    [Required]
    public string ProtocolType { get; set; } = string.Empty;

    public string? NetworkProvider { get; set; }

    public string? FirmwareVersion { get; set; }

    public ActivationStatus ActivationStatus { get; set; }

    public DateTime? WarrantyExpiryDate { get; set; }

    public DateTime? InstalledAt { get; set; }
}