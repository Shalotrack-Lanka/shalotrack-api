namespace ShaloTrack_API.Enums;

public enum ComplaintStatus
{
    // Initial state when the customer has a matched dealer at filing time.
    WithDealer = 0,

    // Initial state when the customer has no matched dealer, OR set when
    // a dealer explicitly escalates a WithDealer complaint they couldn't
    // resolve. DealerId stays populated even after escalation -- it
    // records who the complaint came via, not who currently owns it.
    WithAdmin = 1,

    Resolved = 2,
    Closed = 3
}