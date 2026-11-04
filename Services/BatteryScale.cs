namespace ShaloTrack_API.Services;

/// <summary>
/// What DeviceStatuses.BatteryLevel actually holds, and the one place that turns it into a percentage.
///
/// The gateway stores the tracker's raw byte. For GT06/V5 trackers that byte is a VOLTAGE LEVEL from 0 to 6
/// (6 = full), NOT a percentage: a full battery arrived as "6" and every client showed "6%" and fired
/// "battery below 20%". A GPS103 tracker reports no battery at all and the gateway stores 0 as "not reported".
///
/// So, for display and alerts:
///   0      -> unknown (not reported); never "low", never shown as 0%
///   1..6   -> an approximate percentage from the level (GT06: 1 extremely low, 2 very low [the tracker's own
///             low-battery alarm level], 3 low, 4 medium, 5 high, 6 full)
///   above 6 -> already a percentage (kept, capped at 100); future-proof for trackers that report one
/// The values for 1..6 are approximations of a coarse 7-step scale, not measurements.
/// </summary>
public static class BatteryScale
{
    /// <summary>Same threshold as the Android/Fleet "low battery" wording.</summary>
    public const int LowPercent = 20;

    public static int ToPercent(int raw) => raw switch
    {
        <= 0 => 0,
        1 => 10,
        2 => 15,
        3 => 40,
        4 => 60,
        5 => 80,
        6 => 100,
        _ => Math.Min(raw, 100),
    };

    /// <summary>0 means "not reported" (or a tracker that cannot report one): treated as unknown, not as empty.</summary>
    public static bool IsKnown(int raw) => raw > 0;

    /// <summary>True only for a REAL low reading (level 1 or 2, or a percentage below 20).</summary>
    public static bool IsLow(int raw) => IsKnown(raw) && ToPercent(raw) < LowPercent;
}