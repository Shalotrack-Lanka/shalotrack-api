using ShaloTrack_API.Enums;

namespace ShaloTrack_API.DTOs.DeviceStatus;

public class DeviceStatusResponseDto
{
    public Guid DeviceId { get; set; }
    public string ImeiNumber { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public string? VehicleNumber { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public DateTime? LastSeen { get; set; }
    public int GpsSignal { get; set; }
    public int BatteryLevel { get; set; }
    public bool IgnitionStatus { get; set; }
    public bool MovementStatus { get; set; }
    public PowerStatus PowerStatus { get; set; }
    public DateTime UpdatedAt { get; set; }
}