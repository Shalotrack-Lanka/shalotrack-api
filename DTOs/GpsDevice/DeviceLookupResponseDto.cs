namespace ShaloTrack_API.DTOs.GpsDevice;

/// <summary>
/// Minimal response for customer-facing IMEI lookup during device linking.
/// Deliberately excludes SIM number, firmware, warranty date, etc. — those are
/// admin-only device metadata, not appropriate to hand a regular customer.
/// </summary>
public class DeviceLookupResponseDto
{
    public Guid DeviceId { get; set; }
    public string ImeiNumber { get; set; } = string.Empty;
}