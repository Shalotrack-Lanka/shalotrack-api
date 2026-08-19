namespace ShaloTrack_API.Enums;

public enum VehicleShareStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Revoked = 3 // owner-initiated removal, distinct from the other party declining
}