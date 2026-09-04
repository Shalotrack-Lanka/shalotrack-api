using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IGeofenceRepository
{
    Task<List<Geofence>> GetByCustomerAsync(Guid customerId);

    Task<Geofence?> GetByIdAsync(Guid geofenceId);

    // Used by the real-time detection listener -- returns only active
    // geofences that actually apply to this vehicle: either scoped
    // directly to it, or scoped to none (VehicleId null, meaning "all of
    // this customer's vehicles").
    Task<List<Geofence>> GetActiveForVehicleAsync(Guid customerId, Guid vehicleId);

    // NEW -- for display to a shared viewer, not detection. Unlike
    // GetActiveForVehicleAsync above, this deliberately does NOT filter
    // by IsActive -- a shared viewer should see the full picture
    // (including a disabled geofence) the same way the owner sees their
    // own complete list via GetByCustomerAsync, not a detection-scoped
    // subset.
    Task<List<Geofence>> GetForVehicleAsync(Guid ownerCustomerId, Guid vehicleId);

    Task AddAsync(Geofence geofence);
    void Update(Geofence geofence);
    void Remove(Geofence geofence);
}