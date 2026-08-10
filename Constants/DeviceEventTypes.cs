namespace ShaloTrack_API.Constants;

/// <summary>
/// Mirrors the gateway's own constants/event_types.py -- DeviceEvents is a
/// shared table, and EventType is a plain string column (not a DB-level
/// enum), so the C# side must use the exact same string values the
/// gateway already writes, not invent its own convention.
///
/// Only defines the value this API actually writes today (SOS). The
/// gateway's full enum has many more values (DEVICE_ONLINE, IGNITION_ON,
/// OVERSPEED, etc.) that remain gateway-only for now -- duplicating all of
/// them here would be unused scope, not consistency.
/// </summary>
public static class DeviceEventTypes
{
    public const string SOS = "SOS";
}