namespace ShaloTrack_API.Constants;

/// <summary>Defaults and allowed ranges for the per-vehicle speed and idle alert settings.</summary>
public static class AlertDefaults
{
    /// <summary>What every vehicle used before the limit became configurable.</summary>
    public const int SpeedLimitKmh = 80;
    public const int MinSpeedLimitKmh = 20;
    public const int MaxSpeedLimitKmh = 200;

    /// <summary>Suggested value shown when an owner first switches idle alerts on.</summary>
    public const int IdleMinutes = 10;
    public const int MinIdleMinutes = 3;
    public const int MaxIdleMinutes = 120;

    /// <summary>At or below this speed (km/h) with the engine on counts as idling. GPS jitter keeps a parked car from reading exactly 0.</summary>
    public const decimal IdleSpeedKmh = 2m;
}