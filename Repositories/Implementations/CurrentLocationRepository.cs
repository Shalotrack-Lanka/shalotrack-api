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
        return await _context.CurrentLocations
            .AsNoTracking()
            .Where(c => c.VehicleId == vehicleId)
            .Select(CurrentLocationMappings.ToResponseDto)
            .FirstOrDefaultAsync();
    }

    public async Task<CurrentLocationResponseDto?> GetByDeviceAsync(Guid deviceId)
    {
        return await _context.CurrentLocations
            .AsNoTracking()
            .Where(c => c.DeviceId == deviceId)
            .Select(CurrentLocationMappings.ToResponseDto)
            .FirstOrDefaultAsync();
    }
}