using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IVehicleShareRepository
{
    Task AddAsync(VehicleShare share);

    /// <summary>
    /// Marks a tracked or detached <see cref="VehicleShare"/> as modified so that
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> generates the corresponding UPDATE.
    /// Required because the DbContext is configured with
    /// <c>QueryTrackingBehavior.NoTracking</c> globally — entities returned by
    /// query methods are detached and mutations on them are invisible to the
    /// change tracker unless this is called first.
    /// </summary>
    void Update(VehicleShare share);

    /// <summary>Used to prevent duplicate invites for the same vehicle + person.</summary>
    Task<VehicleShare?> GetByVehicleAndSharedWithAsync(Guid vehicleId, Guid sharedWithCustomerId);

    Task<VehicleShare?> GetByIdAsync(Guid shareId);

    /// <summary>Shares the owner has created, across all their vehicles unless vehicleId narrows it.</summary>
    Task<List<VehicleShare>> GetOwnedSharesAsync(Guid ownerCustomerId, Guid? vehicleId = null);

    /// <summary>Vehicles actively shared TO this person (Accepted status only).</summary>
    Task<List<VehicleShare>> GetSharedWithMeAsync(Guid sharedWithCustomerId);

    /// <summary>Pending invites awaiting this person's response.</summary>
    Task<List<VehicleShare>> GetPendingInvitesForAsync(Guid sharedWithCustomerId);

    /// <summary>
    /// All customers this vehicle is actively (Accepted) shared with --
    /// used to extend SOS/alert push delivery beyond just the owner's own
    /// devices.
    /// </summary>
    Task<List<VehicleShare>> GetAcceptedSharesForVehicleAsync(Guid vehicleId);
}