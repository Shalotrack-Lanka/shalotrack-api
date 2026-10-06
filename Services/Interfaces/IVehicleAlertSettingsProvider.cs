namespace ShaloTrack_API.Services.Interfaces;

/// <summary>The thresholds the live alert listener applies to one vehicle. IdleMinutes null = idle alerts off.</summary>
public sealed record VehicleAlertSettings(int SpeedLimitKmh, int? IdleMinutes);

/// <summary>
/// In-memory, short-lived cache of per-vehicle alert thresholds for the GPS hot path. The
/// listener runs once per GPS ping, so it must never query the database per ping.
/// </summary>
public interface IVehicleAlertSettingsProvider
{
    Task<VehicleAlertSettings> GetAsync(Guid vehicleId);

    /// <summary>Drop the cached value (called after the owner saves) so the change applies on the next ping.</summary>
    void Invalidate(Guid vehicleId);
}