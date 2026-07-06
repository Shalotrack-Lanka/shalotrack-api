using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.DeviceStatus;
using ShaloTrack_API.Mappings;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class DeviceStatusRepository : IDeviceStatusRepository
{
    private readonly ShaloTrackDbContext _context;

    public DeviceStatusRepository(
        ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<DeviceStatusResponseDto>> GetAllAsync()
    {
        return await _context.DeviceStatuses
            .AsNoTracking()
            .OrderByDescending(s => s.UpdatedAt)
            .Select(DeviceStatusMappings.ToResponseDto)
            .ToListAsync();
    }

    public async Task<DeviceStatusResponseDto?> GetByDeviceAsync(Guid deviceId)
    {
        return await _context.DeviceStatuses
            .AsNoTracking()
            .Where(s => s.DeviceId == deviceId)
            .Select(DeviceStatusMappings.ToResponseDto)
            .FirstOrDefaultAsync();
    }

    public async Task<DeviceStatusResponseDto?> GetByVehicleAsync(Guid vehicleId)
    {
        return await _context.DeviceStatuses
            .AsNoTracking()
            .Where(s => s.Device.DeviceAssignments
                .Any(a =>
                    a.VehicleId == vehicleId &&
                    a.Status == Enums.AssignmentStatus.Active))
            .Select(DeviceStatusMappings.ToResponseDto)
            .FirstOrDefaultAsync();
    }
}