namespace ShaloTrack_API.DTOs.VehicleAlertSettings;

public class VehicleAlertSettingsResponseDto
{
    /// <summary>The limit in force (the stored value, or the default when none was set).</summary>
    public int SpeedLimitKmh { get; set; }

    public bool IdleAlertEnabled { get; set; }

    /// <summary>The stored minutes, or the suggested default while idle alerts are off.</summary>
    public int IdleAlertMinutes { get; set; }

    // Bounds, so every client (Fleet, Android, iOS) validates against the server's own rules.
    public int DefaultSpeedLimitKmh { get; set; }
    public int MinSpeedLimitKmh { get; set; }
    public int MaxSpeedLimitKmh { get; set; }
    public int MinIdleMinutes { get; set; }
    public int MaxIdleMinutes { get; set; }
}