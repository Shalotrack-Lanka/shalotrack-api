using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IVehicleReminderRepository
{
    Task<List<VehicleReminder>> GetByVehicleAsync(Guid vehicleId);

    /// <summary>Tracked (for update).</summary>
    Task<VehicleReminder?> GetByVehicleAndTypeAsync(Guid vehicleId, VehicleReminderType type);

    /// <summary>Tracked, with Vehicle loaded (for the ownership check on delete).</summary>
    Task<VehicleReminder?> GetByIdWithVehicleAsync(Guid reminderId);

    Task AddAsync(VehicleReminder reminder);
    void Remove(VehicleReminder reminder);
}