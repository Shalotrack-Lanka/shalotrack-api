using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IVehicleRepository
{
    Task<List<Vehicle>> GetAllAsync();

    Task<Vehicle?> GetByIdAsync(Guid vehicleId);

    Task<List<Vehicle>> GetByCustomerAsync(Guid customerId);

    Task<Vehicle?> GetByVehicleNumberAsync(string vehicleNumber);

    Task<Vehicle?> GetByChassisNumberAsync(string chassisNumber);

    Task<Vehicle?> GetByEngineNumberAsync(string engineNumber);

    Task<bool> ExistsAsync(Guid vehicleId);

    Task AddAsync(Vehicle vehicle);

    void Update(Vehicle vehicle);

    void Delete(Vehicle vehicle);
}