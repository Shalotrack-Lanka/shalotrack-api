using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Constants;
using ShaloTrack_API.Data;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Singleton cache in front of the two nullable columns on Vehicles. Entries live for
/// <see cref="Ttl"/>; saving through the API invalidates immediately (same process), and the
/// TTL bounds staleness if the API is ever run as more than one instance.
/// If the database read fails, the last known value (or the defaults) is used: an alert
/// threshold lookup must never break alert delivery.
/// </summary>
public class VehicleAlertSettingsProvider : IVehicleAlertSettingsProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);
    private static readonly VehicleAlertSettings Defaults = new(AlertDefaults.SpeedLimitKmh, null);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VehicleAlertSettingsProvider> _logger;
    private readonly ConcurrentDictionary<Guid, (VehicleAlertSettings Settings, DateTime ExpiresUtc)> _cache = new();

    public VehicleAlertSettingsProvider(IServiceScopeFactory scopeFactory, ILogger<VehicleAlertSettingsProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<VehicleAlertSettings> GetAsync(Guid vehicleId)
    {
        var now = DateTime.UtcNow;

        if (_cache.TryGetValue(vehicleId, out var hit) && hit.ExpiresUtc > now)
            return hit.Settings;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ShaloTrackDbContext>();

            var row = await db.Vehicles
                .AsNoTracking()
                .Where(v => v.VehicleId == vehicleId)
                .Select(v => new { v.SpeedLimitKmh, v.IdleAlertMinutes })
                .FirstOrDefaultAsync();

            var settings = row is null
                ? Defaults
                : new VehicleAlertSettings(row.SpeedLimitKmh ?? AlertDefaults.SpeedLimitKmh, row.IdleAlertMinutes);

            _cache[vehicleId] = (settings, now + Ttl);
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load alert settings for vehicle {VehicleId}; using the last known value or defaults.", vehicleId);
            return hit.Settings ?? Defaults;
        }
    }

    public void Invalidate(Guid vehicleId) => _cache.TryRemove(vehicleId, out _);
}