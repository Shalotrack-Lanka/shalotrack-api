using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class GpsDeviceRepository : IGpsDeviceRepository
{
    private readonly ShaloTrackDbContext _context;

    public GpsDeviceRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<GpsDevice>> GetAllAsync()
    {
        return await _context.GpsDevices
            .Include(d => d.DeviceAssignments)
            .AsNoTracking()
            .OrderBy(d => d.ImeiNumber)
            .ToListAsync();
    }

    public async Task<GpsDevice?> GetByIdAsync(Guid deviceId)
    {
        return await _context.GpsDevices
            .Include(d => d.DeviceAssignments)
            .FirstOrDefaultAsync(d => d.DeviceId == deviceId);
    }

    public async Task<GpsDevice?> GetByImeiAsync(string imei)
    {
        return await _context.GpsDevices
            .FirstOrDefaultAsync(d => d.ImeiNumber == imei);
    }

    public async Task<GpsDevice?> GetBySimNumberAsync(string simNumber)
    {
        return await _context.GpsDevices
            .FirstOrDefaultAsync(d => d.SimNumber == simNumber);
    }

    public async Task<bool> ExistsAsync(Guid deviceId)
    {
        return await _context.GpsDevices
            .AnyAsync(d => d.DeviceId == deviceId);
    }

    public async Task AddAsync(GpsDevice device)
    {
        await _context.GpsDevices.AddAsync(device);
    }

    public void Update(GpsDevice device)
    {
        _context.GpsDevices.Update(device);
    }

    public void Delete(GpsDevice device)
    {
        _context.GpsDevices.Remove(device);
    }
}