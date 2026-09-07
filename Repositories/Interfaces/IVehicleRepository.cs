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

    Task<List<Vehicle>> GetByCustomerAsync(Guid customerId);

    Task<Vehicle?> GetByVehicleNumberAsync(string vehicleNumber);

    Task<Vehicle?> GetByChassisNumberAsync(string chassisNumber);

    Task<Vehicle?> GetByEngineNumberAsync(string engineNumber);

    Task<bool> ExistsAsync(Guid vehicleId);

    Task AddAsync(Vehicle vehicle);

    void Update(Vehicle vehicle);

    void Delete(Vehicle vehicle);
}