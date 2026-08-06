using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IEmergencyContactRepository
{
    Task<List<EmergencyContact>> GetByCustomerAsync(Guid customerId);
    Task<EmergencyContact?> GetByIdAsync(Guid emergencyContactId);
    Task AddAsync(EmergencyContact contact);
    void Remove(EmergencyContact contact);
}