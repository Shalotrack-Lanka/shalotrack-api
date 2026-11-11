namespace ShaloTrack_API.Services.Interfaces;

public interface ISubscriptionGateService
{
    /// <summary>Error code returned (inside ApiResponse.Errors) with HTTP 402 when renewal is required.</summary>
    const string RenewalRequiredCode = "SUBSCRIPTION_RENEWAL_REQUIRED";

    /// <summary>
    /// While a subscription is unpaid, trip history and trip reports stay available except for the most recent
    /// this-many hours, so history cannot be polled as a free live feed. Live location, the live hub and
    /// live-share links stay fully blocked. Alerts are not gated.
    /// </summary>
    const int UnpaidHistoryHiddenHours = 24;

    /// <summary>
    /// True when LIVE tracking for this vehicle must be withheld (and recent history is hidden, see UnpaidHistoryHiddenHours) because the
    /// subscription of its active device is unpaid or expired. Staff, the demo vehicle,
    /// vehicles with no active device and devices with no synced status row are never blocked
    /// (fails open, so nobody is locked out before the first sync has run).
    /// </summary>
    Task<bool> IsRenewalRequiredAsync(Guid vehicleId);
}