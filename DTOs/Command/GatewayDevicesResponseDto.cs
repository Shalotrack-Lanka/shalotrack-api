namespace ShaloTrack_API.DTOs.Command;

public class GatewayDeviceStatusDto
{
    public string Imei { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastSeen { get; set; }
}

public class GatewayDevicesResponseDto
{
    public List<GatewayDeviceStatusDto> ConnectedDevices { get; set; } = new();
    public int Count { get; set; }
}