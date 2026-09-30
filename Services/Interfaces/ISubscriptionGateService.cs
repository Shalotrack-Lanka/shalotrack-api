namespace ShaloTrack_API.Services.Interfaces;

public interface ISubscriptionGateService
{
    /// <summary>Error code returned (inside ApiResponse.Errors) with HTTP 402 when renewal is required.</summary>
    const string RenewalRequiredCode = "SUBSCRIPTION_RENEWAL_REQUIRED";

    /// <summary>
    /// True when live tracking / history for this vehicle must be withheld because the
    /// subscription of its active device is unpaid or expired. Staff, the demo vehicle,
    /// vehicles with no active device and devices with no synced status row are never blocked
    /// (fails open, so nobody is locked out before the first sync has run).
    /// </summary>
    Task<bool> IsRenewalRequiredAsync(Guid vehicleId);
}