using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class DeviceAssignmentRepository : IDeviceAssignmentRepository
{
    private readonly ShaloTrackDbContext _context;

    public DeviceAssignmentRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<DeviceAssignment>> GetAllAsync()
    {
        return await _context.DeviceAssignments
            .Include(a => a.Vehicle)
            .Include(a => a.Device)
            .AsNoTracking()
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
    }

    public async Task<DeviceAssignment?> GetByIdAsync(Guid assignmentId)
    {
        return await _context.DeviceAssignments
            .Include(a => a.Vehicle)
            .Include(a => a.Device)
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId);
    }

    public async Task<List<DeviceAssignment>> GetByVehicleAsync(
        Guid vehicleId,
        bool activeOnly = false)
    {
        var query = _context.DeviceAssignments
            .Include(a => a.Vehicle)
            .Include(a => a.Device)
            .Where(a => a.VehicleId == vehicleId);

        if (activeOnly)
        {
            query = query.Where(a =>
                a.Status == AssignmentStatus.Active);
        }

        return await query
            .AsNoTracking()
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
    }

    public async Task<List<DeviceAssignment>> GetByDeviceAsync(
        Guid deviceId,
        bool activeOnly = false)
    {
        var query = _context.DeviceAssignments
            .Include(a => a.Vehicle)
            .Include(a => a.Device)
            .Where(a => a.DeviceId == deviceId);

        if (activeOnly)
        {
            query = query.Where(a =>
                a.Status == AssignmentStatus.Active);
        }

        return await query
            .AsNoTracking()
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync();
    }

    public async Task AddAsync(DeviceAssignment assignment)
    {
        await _context.DeviceAssignments.AddAsync(assignment);
    }

    public void Update(DeviceAssignment assignment)
    {
        _context.DeviceAssignments.Update(assignment);
    }
}