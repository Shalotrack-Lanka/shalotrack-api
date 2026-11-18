using ShaloTrack_API.DTOs.Dashboard;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface ICustomerRepository
{
    Task<List<Customer>> GetAllAsync();
    Task<Customer?> GetByIdAsync(Guid customerId);
    Task<Customer?> GetByEmailAsync(string email);
    Task<Customer?> GetByNicAsync(string nicNumber);
    Task<bool> ExistsAsync(Guid customerId);
    Task AddAsync(Customer customer);
    void Update(Customer customer);
    void Delete(Customer customer);
    Task<Customer?> GetByFirebaseUidAsync(string firebaseUid);
    Task<int> CountActiveVehiclesAsync(Guid customerId);
    Task<Customer?> GetByPhoneNumberAsync(string phoneNumber);
    Task<DashboardResponseDto?> GetDashboardAsync(Guid customerId);

    /// <summary>
    /// For each vehicle with an ACTIVE device assignment: the device id and the gateway's connection
    /// record (IsOnline, last seen, last heartbeat). One query for the whole list.
    /// Vehicles with no active assignment are not in the result.
    /// </summary>
    Task<Dictionary<Guid, VehicleConnection>> GetVehicleConnectionsAsync(IReadOnlyCollection<Guid> vehicleIds);
}

/// <summary>Connection facts for one vehicle's active tracker. Status fields are null when the gateway has no status row yet.</summary>
public sealed record VehicleConnection(Guid DeviceId, bool? IsOnline, DateTime? LastSeen, DateTime? LastHeartbeat, bool PowerDisconnected);