namespace ShaloTrack_API.Enums;

/// <summary>
/// What a vehicle reminder is for. Stored as an int, so NEVER reorder or renumber -- only append.
/// ServiceDue is date-based only: a km-based service interval needs a trustworthy odometer, which
/// the GPS data does not provide.
/// </summary>
public enum VehicleReminderType
{
    RevenueLicence = 0,
    Insurance = 1,
    ServiceDue = 2,
}