using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IDeviceAssignmentRepository
{
    Task<List<DeviceAssignment>> GetAllAsync();

    Task<DeviceAssignment?> GetByIdAsync(Guid assignmentId);

    Task<List<DeviceAssignment>> GetByVehicleAsync(
        Guid vehicleId,
        bool activeOnly = false);

    Task<List<DeviceAssignment>> GetByDeviceAsync(
        Guid deviceId,
        bool activeOnly = false);

    Task AddAsync(DeviceAssignment assignment);

    void Update(DeviceAssignment assignment);
}