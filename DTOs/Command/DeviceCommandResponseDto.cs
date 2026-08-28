namespace ShaloTrack_API.DTOs.Command;

public class DeviceCommandResponseDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Imei { get; set; } = string.Empty;
}