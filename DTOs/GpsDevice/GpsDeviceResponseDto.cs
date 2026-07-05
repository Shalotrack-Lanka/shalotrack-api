using ShaloTrack_API.Enums;

namespace ShaloTrack_API.DTOs.GpsDevice;

public class GpsDeviceResponseDto
{
    public Guid DeviceId { get; set; }

    public string ImeiNumber { get; set; } = string.Empty;

    public string? SimNumber { get; set; }

    public string DeviceModel { get; set; } = string.Empty;

    public string ProtocolType { get; set; } = string.Empty;

    public string? NetworkProvider { get; set; }

    public string? FirmwareVersion { get; set; }

    public ActivationStatus ActivationStatus { get; set; }

    public DateTime? WarrantyExpiryDate { get; set; }

    public DateTime? InstalledAt { get; set; }

    public bool IsAssigned { get; set; }
}