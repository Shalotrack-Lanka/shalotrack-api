namespace ShaloTrack_API.Enums;

/// <summary>
/// AwaitingSlip and PendingReview are the two "open" states: at most one open request per vehicle
/// (enforced by a unique partial index, see ShaloTrackDbContext). Do not renumber: the index filter
/// and stored rows depend on these values.
/// </summary>
public enum RenewalStatus
{
    AwaitingSlip = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}