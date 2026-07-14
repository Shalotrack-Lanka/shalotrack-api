using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.GpsTracking;
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
            .Include(d => d.DeviceAssignments) // to check the assignment status of the gps device
            .FirstOrDefaultAsync(d => d.ImeiNumber == imei);
    }

    public async Task<GpsDevice?> GetBySimNumberAsync(string simNumber)
    {
        return await _context.GpsDevices
            .FirstOrDefaultAsync(d => d.SimNumber == simNumber);
    }

    public async Task<List<TrackingPointRaw>> GetPointsForTripsAsync(Guid vehicleId, DateTime from, DateTime to)
    {
        // Unpaged, ascending order — needed to walk the sequence correctly for trip
        // detection. Never exposed to the client directly; only the computed summary is.
        return await _context.GpsTrackings
            .AsNoTracking()
            .Where(x => x.Device.DeviceAssignments.Any(a =>
                a.VehicleId == vehicleId && a.Status == Enums.AssignmentStatus.Active))
            .Where(x => x.EventTime >= from && x.EventTime <= to)
            .OrderBy(x => x.EventTime)
            .Select(x => new TrackingPointRaw
            {
                EventTime = x.EventTime,
                Latitude = x.Latitude,
                Longitude = x.Longitude,
                Speed = x.Speed
            })
            .ToListAsync();
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