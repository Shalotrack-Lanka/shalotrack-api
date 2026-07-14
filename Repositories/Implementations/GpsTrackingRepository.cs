using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Mappings;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class GpsTrackingRepository : IGpsTrackingRepository
{
    private readonly ShaloTrackDbContext _context;

    public GpsTrackingRepository(
        ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<GpsTrackingResponseDto>> GetAsync(
        GpsTrackingFilter filter)
    {
        IQueryable<Models.GpsTracking> query =
            _context.GpsTrackings
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
                x.Device.DeviceAssignments.Any(a =>
                    a.VehicleId == filter.VehicleId.Value &&
                    a.Status == Enums.AssignmentStatus.Active));
        }

        // Date From
        if (filter.From.HasValue)
        {
            query = query.Where(x =>
                x.EventTime >= filter.From.Value);
        }

        // Date To
        if (filter.To.HasValue)
        {
            query = query.Where(x =>
                x.EventTime <= filter.To.Value);
        }

        query = query
            .OrderByDescending(x => x.EventTime);

        query = query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize);

        return await query
            .Select(GpsTrackingMappings.ToResponseDto)
            .ToListAsync();
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
}