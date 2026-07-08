using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.DeviceEvent;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Mappings;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class DeviceEventRepository : IDeviceEventRepository
{
    private readonly ShaloTrackDbContext _context;

    public DeviceEventRepository(
        ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<DeviceEventResponseDto>> GetAsync(
        DeviceEventFilter filter)
    {
        IQueryable<Models.DeviceEvent> query =
            _context.DeviceEvents
                .AsNoTracking();

        // Device Filter
        if (filter.DeviceId.HasValue)
        {
            query = query.Where(x =>
                x.DeviceId == filter.DeviceId.Value);
        }

        // Vehicle Filter
        if (filter.VehicleId.HasValue)
        {
            query = query.Where(x =>
                x.VehicleId == filter.VehicleId.Value);
        }

        // Event Type Filter
        if (!string.IsNullOrWhiteSpace(filter.EventType))
        {
            query = query.Where(x =>
                x.EventType == filter.EventType);
        }

        // Severity Filter
        if (filter.Severity.HasValue)
        {
            query = query.Where(x =>
                x.Severity == filter.Severity.Value);
        }

        // Date From
        if (filter.FromDate.HasValue)
        {
            query = query.Where(x =>
                x.CreatedAt >= filter.FromDate.Value);
        }

        // Date To
        if (filter.ToDate.HasValue)
        {
            query = query.Where(x =>
                x.CreatedAt <= filter.ToDate.Value);
        }

        query = query
            .OrderByDescending(x => x.CreatedAt);

        query = query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize);

        return await query
            .Select(DeviceEventMappings.ToResponseDto)
            .ToListAsync();
    }

    public async Task<DeviceEventResponseDto?> GetByIdAsync(
        long eventId)
    {
        return await _context.DeviceEvents
            .AsNoTracking()
            .Where(x => x.EventId == eventId)
            .Select(DeviceEventMappings.ToResponseDto)
            .FirstOrDefaultAsync();
    }
}