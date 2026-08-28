namespace ShaloTrack_API.DTOs.Command;

public class SendDeviceCommandDto
{
    public string Command { get; set; } = string.Empty;
    public Dictionary<string, object>? Params { get; set; }
}