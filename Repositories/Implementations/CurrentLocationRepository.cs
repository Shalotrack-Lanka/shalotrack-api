using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.CurrentLocation;
using ShaloTrack_API.Mappings;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class CurrentLocationRepository : ICurrentLocationRepository
{
    private readonly ShaloTrackDbContext _context;

    public CurrentLocationRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<CurrentLocationResponseDto>> GetAllAsync()
    {
        return await _context.CurrentLocations
            .AsNoTracking()
            .OrderByDescending(c => c.LastUpdate)
            .Select(CurrentLocationMappings.ToResponseDto)
            .ToListAsync();
    }

    public async Task<CurrentLocationResponseDto?> GetByVehicleAsync(Guid vehicleId)
    {
        // PRIVACY: a position recorded before the device was bound to this vehicle belongs to its
        // previous owner, so it is not returned (the vehicle then shows "no fix yet" until the
        // device reports again). The demo vehicle is exempt.
        return await _context.CurrentLocations
            .AsNoTracking()
            .Where(c => c.VehicleId == vehicleId)
            .Where(c => c.Vehicle.IsDemoVehicle ||
                        c.Device.DeviceAssignments.Any(a =>
                            a.VehicleId == vehicleId &&
                            a.Status == Enums.AssignmentStatus.Active &&
                            c.LastUpdate >= a.AssignedAt))
            .Select(CurrentLocationMappings.ToResponseDto)
            .FirstOrDefaultAsync();
    }

    public async Task<CurrentLocationResponseDto?> GetByDeviceAsync(Guid deviceId)
    {
        // PRIVACY: same rule as GetByVehicleAsync.
        return await _context.CurrentLocations
            .AsNoTracking()
            .Where(c => c.DeviceId == deviceId)
            .Where(c => c.Vehicle.IsDemoVehicle ||
                        c.Device.DeviceAssignments.Any(a =>
                            a.VehicleId == c.VehicleId &&
                            a.Status == Enums.AssignmentStatus.Active &&
                            c.LastUpdate >= a.AssignedAt))
            .Select(CurrentLocationMappings.ToResponseDto)
            .FirstOrDefaultAsync();
    }

    public async Task RemoveByDeviceAsync(Guid deviceId)
    {
        var row = await _context.CurrentLocations.FirstOrDefaultAsync(c => c.DeviceId == deviceId);
        if (row is not null)
            _context.CurrentLocations.Remove(row);
    }
}