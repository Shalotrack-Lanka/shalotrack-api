namespace ShaloTrack_API.DTOs.VehicleAlertSettings;

public class UpdateVehicleAlertSettingsDto
{
    public int SpeedLimitKmh { get; set; }

    public bool IdleAlertEnabled { get; set; }

    /// <summary>Only used when IdleAlertEnabled is true; ignored otherwise.</summary>
    public int IdleAlertMinutes { get; set; }
}