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

    public GpsTrackingRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<GpsTrackingResponseDto>> GetAsync(GpsTrackingFilter filter)
    {
        IQueryable<Models.GpsTracking> query = _context.GpsTrackings.AsNoTracking();

        if (filter.DeviceId.HasValue)
            query = query.Where(x => x.DeviceId == filter.DeviceId.Value);

        if (filter.VehicleId.HasValue)
            query = query.Where(x =>
                x.Device.DeviceAssignments.Any(a =>
                    a.VehicleId == filter.VehicleId.Value &&
                    a.Status == Enums.AssignmentStatus.Active));

        if (filter.From.HasValue)
            query = query.Where(x => x.EventTime >= filter.From.Value);

        if (filter.To.HasValue)
            query = query.Where(x => x.EventTime <= filter.To.Value);

        query = query.OrderByDescending(x => x.EventTime)
                     .Skip((filter.Page - 1) * filter.PageSize)
                     .Take(filter.PageSize);

        return await query.Select(GpsTrackingMappings.ToResponseDto).ToListAsync();
    }

    /// <inheritdoc cref="IGpsTrackingRepository.GetPointsForTripsAsync"/>
    public async Task<List<TrackingPointRaw>> GetPointsForTripsAsync(
        Guid vehicleId, DateTime from, DateTime to, bool isDemoVehicle = false)
    {
        IQueryable<Models.GpsTracking> query = _context.GpsTrackings.AsNoTracking();

        // For the demo vehicle we intentionally skip the Active-status check:
        // the demo must show GPS history regardless of the current assignment
        // lifecycle so that every customer can see all platform capabilities.
        query = isDemoVehicle
            ? query.Where(x => x.Device.DeviceAssignments.Any(a => a.VehicleId == vehicleId))
            : query.Where(x => x.Device.DeviceAssignments.Any(a =>
                  a.VehicleId == vehicleId && a.Status == Enums.AssignmentStatus.Active));

        return await query
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

    public async Task<int> CountByDeviceInRangeAsync(Guid deviceId, DateTime from, DateTime to)
    {
        return await _context.GpsTrackings
            .Where(x => x.DeviceId == deviceId && x.EventTime >= from && x.EventTime <= to)
            .CountAsync();
    }

    public async Task<int> DeleteByDeviceInRangeAsync(Guid deviceId, DateTime from, DateTime to)
    {
        return await _context.GpsTrackings
            .Where(x => x.DeviceId == deviceId && x.EventTime >= from && x.EventTime <= to)
            .ExecuteDeleteAsync();
    }
}