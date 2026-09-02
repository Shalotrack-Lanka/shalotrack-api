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

    Task AddAsync(Geofence geofence);
    void Update(Geofence geofence);
    void Remove(Geofence geofence);
}