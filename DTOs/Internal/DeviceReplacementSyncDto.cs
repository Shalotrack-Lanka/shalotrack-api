namespace ShaloTrack_API.DTOs.Internal;

public class DeviceReplacementSyncDto
{
    public string OldImei { get; set; } = string.Empty;
    public string NewImei { get; set; } = string.Empty;
}