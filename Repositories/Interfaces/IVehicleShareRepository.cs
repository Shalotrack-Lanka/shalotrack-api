using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IVehicleShareRepository
{
    Task AddAsync(VehicleShare share);
    Task<VehicleShare?> GetByIdAsync(Guid shareId);

    /// <summary>Used to prevent duplicate invites for the same vehicle + person.</summary>
    Task<VehicleShare?> GetByVehicleAndSharedWithAsync(Guid vehicleId, Guid sharedWithCustomerId);

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