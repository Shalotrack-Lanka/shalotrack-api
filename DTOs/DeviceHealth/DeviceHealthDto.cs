namespace ShaloTrack_API.DTOs.DeviceHealth;

/// <summary>
/// One tracker's health as last reported to the gateway. Read by the admin portal's
/// "Device health" worklist; contains no customer data, only the vehicle number the device is on.
/// </summary>
public class DeviceHealthDto
{
    public string Imei { get; set; } = string.Empty;
    public string VehicleNumber { get; set; } = string.Empty;

    /// <summary>
    /// Latest of LastSeen and LastHeartbeat, in UTC. Null = the gateway has never recorded
    /// a status for this device. Deliberately NOT IsOnline: the gateway only clears that on a
    /// clean disconnect, so a gateway crash would leave every device "online".
    /// </summary>
    public DateTime? LastContactUtc { get; set; }

    /// <summary>"Connected" or "Disconnected" (the tracker's external power, not the app).</summary>
    public string? Power { get; set; }

    public int? BatteryLevel { get; set; }
    public int? GpsSignal { get; set; }
}