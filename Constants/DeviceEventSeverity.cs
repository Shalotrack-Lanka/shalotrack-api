namespace ShaloTrack_API.Constants;

/// <summary>
/// Mirrors the gateway's own constants/severity.py exactly (LOW=1,
/// MEDIUM=2, HIGH=3, CRITICAL=4). Defined here as a real constant, not a
/// bare literal 4 in SOSService, so the meaning is visible at the call
/// site and stays in sync if the gateway's scale ever changes.
/// </summary>
public static class DeviceEventSeverity
{
    public const short Low = 1;
    public const short Medium = 2;
    public const short High = 3;
    public const short Critical = 4;
}