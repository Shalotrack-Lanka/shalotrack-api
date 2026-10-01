using ShaloTrack_API.DTOs.DeviceHealth;

namespace ShaloTrack_API.Services.Interfaces;

public interface IDeviceHealthService
{
    /// <summary>
    /// Health of every tracker that is actively assigned to a real (active, non-demo) vehicle.
    /// Read-only; one row per IMEI.
    /// </summary>
    Task<IReadOnlyList<DeviceHealthDto>> GetFleetHealthAsync(CancellationToken cancellationToken = default);
}