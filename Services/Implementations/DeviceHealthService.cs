using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.DeviceHealth;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Fleet health for the admin portal. DeviceStatuses has exactly one row per device (the
/// gateway upserts it), so the left join below cannot multiply rows.
/// </summary>
public class DeviceHealthService : IDeviceHealthService
{
    private readonly ShaloTrackDbContext _db;

    public DeviceHealthService(ShaloTrackDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DeviceHealthDto>> GetFleetHealthAsync(CancellationToken cancellationToken = default)
    {
        var rows = await (
                from a in _db.DeviceAssignments
                join d in _db.GpsDevices on a.DeviceId equals d.DeviceId
                join v in _db.Vehicles on a.VehicleId equals v.VehicleId
                join s in _db.DeviceStatuses on d.DeviceId equals s.DeviceId into statuses
                from s in statuses.DefaultIfEmpty()
                where a.Status == AssignmentStatus.Active
                      && v.IsActive
                      && !v.IsDemoVehicle
                select new
                {
                    d.ImeiNumber,
                    v.VehicleNumber,
                    HasStatus = s != null,
                    LastSeen = s != null ? s.LastSeen : null,
                    LastHeartbeat = s != null ? s.LastHeartbeat : null,
                    Power = s != null ? (PowerStatus?)s.PowerStatus : null,
                    Battery = s != null ? (int?)s.BatteryLevel : null,
                    Gps = s != null ? (int?)s.GpsSignal : null
                })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ImeiNumber)
            .Select(g => g.First())
            .Select(r => new DeviceHealthDto
            {
                Imei = r.ImeiNumber,
                VehicleNumber = r.VehicleNumber,
                LastContactUtc = Latest(r.LastSeen, r.LastHeartbeat),
                Power = r.Power?.ToString(),
                BatteryLevel = r.Battery,
                GpsSignal = r.Gps
            })
            .ToList();
    }

    private static DateTime? Latest(DateTime? a, DateTime? b)
    {
        var latest = a.HasValue && b.HasValue ? (a > b ? a : b) : (a ?? b);

        // Stored as UTC; mark it so it serialises with a trailing Z.
        return latest.HasValue ? DateTime.SpecifyKind(latest.Value, DateTimeKind.Utc) : null;
    }
}