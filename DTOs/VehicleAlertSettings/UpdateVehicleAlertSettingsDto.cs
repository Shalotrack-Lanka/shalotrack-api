namespace ShaloTrack_API.DTOs.VehicleAlertSettings;

public class UpdateVehicleAlertSettingsDto
{
    public int SpeedLimitKmh { get; set; }

    public bool IdleAlertEnabled { get; set; }

    /// <summary>Only used when IdleAlertEnabled is true; ignored otherwise.</summary>
    public int IdleAlertMinutes { get; set; }

    // Push on/off. Nullable on purpose: a client that does not send them (an older app, or Fleet before it
    // knew about them) leaves the stored values alone instead of muting everything with a default false.
    public bool? PushPowerCut { get; set; }
    public bool? PushIgnition { get; set; }
    public bool? PushOverspeed { get; set; }
    public bool? PushTrackerHealth { get; set; }
}