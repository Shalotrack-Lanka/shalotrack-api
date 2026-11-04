using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public sealed record PublicVehicleInfo(string VehicleNumber, bool IsActive, bool IsDemoVehicle, string? VehicleType = null);

public sealed record TrailRow(decimal Latitude, decimal Longitude, DateTime EventTime);

public interface ILiveShareLinkRepository
{
    Task<int> CountActiveAsync(Guid vehicleId, DateTime nowUtc);

    /// <summary>No-tracking, newest first.</summary>
    Task<List<LiveShareLink>> GetActiveByVehicleAsync(Guid vehicleId, DateTime nowUtc);

    /// <summary>
    /// By id. NOT tracked: the DbContext is no-tracking by default (see DatabaseExtensions), so changing the
    /// returned entity and calling SaveChanges saves nothing. To stop a link use <see cref="RevokeAsync"/>.
    /// </summary>
    Task<LiveShareLink?> GetByIdAsync(Guid linkId);

    /// <summary>
    /// Stops a link: sets RevokedAt in one UPDATE (no tracking needed, safe to repeat, never moves an earlier
    /// RevokedAt). Returns the number of rows changed (0 when it was already stopped).
    /// </summary>
    Task<int> RevokeAsync(Guid linkId, DateTime nowUtc);

    /// <summary>No-tracking, by the SHA-256 hex of the token.</summary>
    Task<LiveShareLink?> GetByTokenHashAsync(string tokenHash);

    Task AddAsync(LiveShareLink link);

    Task<PublicVehicleInfo?> GetVehicleInfoAsync(Guid vehicleId);

    /// <summary>The vehicle's current position row (no-tracking), or null if it has none yet.</summary>
    Task<CurrentLocation?> GetLocationAsync(Guid vehicleId);

    /// <summary>
    /// The most recent points of one device at or after sinceUtc, returned oldest first, capped at
    /// maxRows (newest kept). Uses the (DeviceId, EventTime) index.
    /// </summary>
    Task<List<TrailRow>> GetTrailAsync(Guid deviceId, DateTime sinceUtc, int maxRows);
}