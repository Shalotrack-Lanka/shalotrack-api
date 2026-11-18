using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IVehicleRepository
{
    Task<List<Vehicle>> GetAllAsync();

    Task<Vehicle?> GetByIdAsync(Guid vehicleId);

    /// <summary>
    /// Lightweight ownership check — loads Customer (for FirebaseUid) only.
    /// Does NOT load DeviceAssignments or Device. Use this in service-layer
    /// ownership checks where the IMEI and assignment history are not needed.
    /// Avoids loading the full assignment history on every authorization check.
    /// </summary>
    Task<Vehicle?> GetByIdForOwnershipCheckAsync(Guid vehicleId);

    /// <summary>
    /// Writes a vehicle's alert settings in ONE UPDATE, only on the owner's own active, non-demo vehicle.
    /// Runs immediately (no SaveChanges). The context uses NoTracking by default, so changing a loaded
    /// entity and calling SaveChanges would write nothing. Returns the number of rows changed (0 or 1).
    /// </summary>
    Task<int> UpdateAlertSettingsAsync(Guid vehicleId, Guid customerId, int? speedLimitKmh, int? idleAlertMinutes,
        bool pushPowerCut, bool pushIgnition, bool pushOverspeed, bool pushTrackerHealth, DateTime updatedAtUtc);

    Task<List<Vehicle>> GetByCustomerAsync(Guid customerId);

    Task<Vehicle?> GetByVehicleNumberAsync(string vehicleNumber);

    // NEW -- the one, shared demo vehicle visible to every customer.
    // Null if none has been designated yet.
    Task<Vehicle?> GetDemoVehicleAsync();

    Task<Vehicle?> GetByChassisNumberAsync(string chassisNumber);

    Task<Vehicle?> GetByEngineNumberAsync(string engineNumber);

    Task<bool> ExistsAsync(Guid vehicleId);

    Task AddAsync(Vehicle vehicle);

    void Update(Vehicle vehicle);

    void Delete(Vehicle vehicle);
}