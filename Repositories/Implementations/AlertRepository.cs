using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class AlertRepository : IAlertRepository
{
    private readonly ShaloTrackDbContext _context;

    public AlertRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<Alert>> GetByCustomerAsync(Guid customerId, int page, int pageSize, Guid? vehicleId = null)
    {
        var query = _context.Alerts
            .AsNoTracking()
            .Include(a => a.Vehicle)
            .Where(a => a.Vehicle.CustomerId == customerId);

        // NEW: still combined with the CustomerId filter above, so a
        // vehicleId belonging to a different customer just yields an
        // empty result rather than exposing anything.
        if (vehicleId.HasValue)
        {
            query = query.Where(a => a.VehicleId == vehicleId.Value);
        }

        return await query
            .OrderByDescending(a => a.TriggeredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<Alert?> GetByIdAsync(long alertId)
    {
        return await _context.Alerts
            .Include(a => a.Vehicle)
                .ThenInclude(v => v.Customer)
            .FirstOrDefaultAsync(a => a.AlertId == alertId);
    }

    public async Task AddAsync(Alert alert)
    {
        await _context.Alerts.AddAsync(alert);
    }

    public async Task<Alert?> GetMostRecentByDeviceAndTypeAsync(Guid deviceId, AlertType alertType, DateTime before)
    {
        return await _context.Alerts
            .AsNoTracking()
            .Where(a => a.DeviceId == deviceId && a.AlertType == alertType && a.TriggeredAt < before)
            .OrderByDescending(a => a.TriggeredAt)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ExistsByDeviceAndTypeBetweenAsync(Guid deviceId, AlertType alertType, DateTime after, DateTime before)
    {
        return await _context.Alerts
            .AsNoTracking()
            .AnyAsync(a => a.DeviceId == deviceId
                && a.AlertType == alertType
                && a.TriggeredAt > after
                && a.TriggeredAt < before);
    }

    // NEW -- for Value/stats.
    public async Task<int> CountByVehicleAndTypeAsync(Guid vehicleId, AlertType alertType, DateTime from, DateTime to)
    {
        return await _context.Alerts
            .AsNoTracking()
            .CountAsync(a => a.VehicleId == vehicleId
                && a.AlertType == alertType
                && a.TriggeredAt >= from
                && a.TriggeredAt <= to);
    }
}