using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IVehicleShareRepository
{
    Task AddAsync(VehicleShare share);

    /// <summary>
    /// True when this vehicle already has a live pending invite to this number, or an accepted share with this
    /// number or with the customer who holds it now. Used to refuse a duplicate invite.
    /// </summary>
    Task<bool> HasActiveShareAsync(Guid vehicleId, string invitedE164, Guid? currentHolderCustomerId, DateTime pendingSince);

    /// <summary>Live (not expired) pending invites this owner has sent, across all vehicles.</summary>
    Task<int> CountPendingInvitesAsync(Guid ownerCustomerId, DateTime pendingSince);

    /// <summary>
    /// Accepts or declines in one UPDATE. Matches only a live pending invite addressed to the responder's number,
    /// for an active vehicle they don't own. Returns rows changed (0 = not found, expired or already answered).
    /// </summary>
    Task<int> RespondAsync(Guid shareId, Guid responderCustomerId, string responderE164, bool accept, DateTime pendingSince, DateTime now);

    /// <summary>Owner ends a pending or accepted share in one UPDATE. Returns rows changed.</summary>
    Task<int> RevokeAsync(Guid shareId, Guid ownerCustomerId, DateTime now);

    /// <summary>The ACCEPTED share linking this customer to this vehicle, if any. Used by every access check.</summary>
    Task<VehicleShare?> GetByVehicleAndSharedWithAsync(Guid vehicleId, Guid sharedWithCustomerId);

    Task<VehicleShare?> GetByIdAsync(Guid shareId);

    /// <summary>Shares the owner has created, across all their vehicles unless vehicleId narrows it.</summary>
    Task<List<VehicleShare>> GetOwnedSharesAsync(Guid ownerCustomerId, Guid? vehicleId = null);

    /// <summary>Vehicles actively shared TO this person (Accepted status only).</summary>
    Task<List<VehicleShare>> GetSharedWithMeAsync(Guid sharedWithCustomerId);

    /// <summary>Live pending invites addressed to this person's number (never for their own vehicles).</summary>
    Task<List<VehicleShare>> GetPendingInvitesForAsync(Guid viewerCustomerId, string viewerE164, DateTime pendingSince);

    /// <summary>
    /// All customers this vehicle is actively (Accepted) shared with --
    /// used to extend SOS/alert push delivery beyond just the owner's own
    /// devices.
    /// </summary>
    Task<List<VehicleShare>> GetAcceptedSharesForVehicleAsync(Guid vehicleId);
}