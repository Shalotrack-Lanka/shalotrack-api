using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class GeofenceRepository : IGeofenceRepository
{
    private readonly ShaloTrackDbContext _context;

    public GeofenceRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<Geofence>> GetByCustomerAsync(Guid customerId)
    {
        return await _context.Geofences
            .AsNoTracking()
            .Include(g => g.Vehicle)
            .Where(g => g.CustomerId == customerId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<Geofence?> GetByIdAsync(Guid geofenceId)
    {
        return await _context.Geofences
            .Include(g => g.Vehicle)
            .FirstOrDefaultAsync(g => g.GeofenceId == geofenceId);
    }

    public async Task<List<Geofence>> GetActiveForVehicleAsync(Guid customerId, Guid vehicleId)
    {
        return await _context.Geofences
            .AsNoTracking()
            .Where(g => g.CustomerId == customerId
                && g.IsActive
                && (g.VehicleId == null || g.VehicleId == vehicleId))
            .ToListAsync();
    }

    public async Task<List<Geofence>> GetForVehicleAsync(Guid ownerCustomerId, Guid vehicleId)
    {
        return await _context.Geofences
            .AsNoTracking()
            .Include(g => g.Vehicle)
            .Where(g => g.CustomerId == ownerCustomerId
                && (g.VehicleId == null || g.VehicleId == vehicleId))
            .ToListAsync();
    }

    public async Task AddAsync(Geofence geofence)
    {
        await _context.Geofences.AddAsync(geofence);
    }

    public void Update(Geofence geofence)
    {
        _context.Geofences.Update(geofence);
    }

    public void Remove(Geofence geofence)
    {
        _context.Geofences.Remove(geofence);
    }
}