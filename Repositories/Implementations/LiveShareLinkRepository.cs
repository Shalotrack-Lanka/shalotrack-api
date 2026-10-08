using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class LiveShareLinkRepository : ILiveShareLinkRepository
{
    private readonly ShaloTrackDbContext _context;

    public LiveShareLinkRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<int> CountActiveAsync(Guid vehicleId, DateTime nowUtc)
    {
        return await _context.LiveShareLinks
            .CountAsync(l => l.VehicleId == vehicleId && l.RevokedAt == null && l.ExpiresAt > nowUtc);
    }

    public async Task<List<LiveShareLink>> GetActiveByVehicleAsync(Guid vehicleId, DateTime nowUtc)
    {
        return await _context.LiveShareLinks
            .AsNoTracking()
            .Where(l => l.VehicleId == vehicleId && l.RevokedAt == null && l.ExpiresAt > nowUtc)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();
    }

    public async Task<LiveShareLink?> GetByIdAsync(Guid linkId)
    {
        return await _context.LiveShareLinks.FirstOrDefaultAsync(l => l.LinkId == linkId);
    }

    public async Task<LiveShareLink?> GetByTokenHashAsync(string tokenHash)
    {
        return await _context.LiveShareLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TokenHash == tokenHash);
    }

    public async Task AddAsync(LiveShareLink link)
    {
        await _context.LiveShareLinks.AddAsync(link);
    }

    public async Task<PublicVehicleInfo?> GetVehicleInfoAsync(Guid vehicleId)
    {
        return await _context.Vehicles
            .AsNoTracking()
            .Where(v => v.VehicleId == vehicleId)
            .Select(v => new PublicVehicleInfo(v.VehicleNumber, v.IsActive, v.IsDemoVehicle, v.VehicleType))
            .FirstOrDefaultAsync();
    }

    public async Task<CurrentLocation?> GetLocationAsync(Guid vehicleId)
    {
        return await _context.CurrentLocations
            .AsNoTracking()
            .Where(c => c.VehicleId == vehicleId)
            .OrderByDescending(c => c.LastUpdate)
            .FirstOrDefaultAsync();
    }

    public async Task<List<TrailRow>> GetTrailAsync(Guid deviceId, DateTime sinceUtc, int maxRows)
    {
        var newestFirst = await _context.GpsTrackings
            .AsNoTracking()
            .Where(g => g.DeviceId == deviceId && g.EventTime >= sinceUtc)
            .OrderByDescending(g => g.EventTime)
            .Take(maxRows)
            .Select(g => new TrailRow(g.Latitude, g.Longitude, g.EventTime))
            .ToListAsync();

        newestFirst.Reverse();
        return newestFirst;
    }
}