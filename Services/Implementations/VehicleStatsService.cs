using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.DTOs.VehicleStats;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class VehicleStatsService : IVehicleStatsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IGpsTrackingService _gpsTrackingService;

    // Sri Lanka Standard Time -- fixed UTC+5:30, no DST, matching the same
    // conversion already used elsewhere in this project (the gateway's own
    // logger does the same thing for display purposes).
    private static readonly TimeSpan SriLankaOffset = TimeSpan.FromHours(5.5);

    public VehicleStatsService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IGpsTrackingService gpsTrackingService)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _gpsTrackingService = gpsTrackingService;
    }

    public async Task<ApiResponse<VehicleStatsResponseDto>> GetStatsAsync(Guid vehicleId, string? period)
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<VehicleStatsResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
        if (customer is null)
        {
            return ApiResponse<VehicleStatsResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);
        if (vehicle is null || (!_currentUser.IsStaff && vehicle.CustomerId != customer.CustomerId))
        {
            return ApiResponse<VehicleStatsResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Vehicle not found.", $"No vehicle exists with ID '{vehicleId}'.");
        }

        var normalizedPeriod = (period ?? "today").ToLowerInvariant();
        var (from, to) = ResolvePeriodRange(normalizedPeriod, vehicle.CreatedAt);

        var tripsResult = await _gpsTrackingService.GetTripsSummaryAsync(vehicleId, from, to);
        if (tripsResult.Data is null)
        {
            return ApiResponse<VehicleStatsResponseDto>.Fail(
                tripsResult.StatusCode, tripsResult.Message, "Could not compute trip data for stats.");
        }

        var report = tripsResult.Data;

        decimal totalDistanceKm = report.Trips.Sum(t => t.DistanceKm);
        decimal totalIdleMinutes = report.Stops.Sum(s => s.DurationMinutes);
        decimal totalDrivingMinutes = report.Trips.Sum(t => t.DurationMinutes);
        decimal maxSpeed = report.Trips.Count > 0 ? report.Trips.Max(t => t.MaxSpeed) : 0;

        decimal averageSpeed = totalDrivingMinutes > 0
            ? report.Trips.Sum(t => t.AvgSpeed * t.DurationMinutes) / totalDrivingMinutes
            : 0;

        int overspeedIncidentCount = await _unitOfWork.Alerts.CountByVehicleAndTypeAsync(
            vehicleId, AlertType.Overspeed, from, to);

        var dailyBreakdown = BuildDailyBreakdown(report.Trips, report.Stops, from, to);

        var stats = new VehicleStatsResponseDto
        {
            VehicleId = vehicleId,
            Period = normalizedPeriod,
            PeriodFrom = from,
            PeriodTo = to,
            TotalDistanceKm = totalDistanceKm,
            TotalTripCount = report.TripCount,
            TotalStopCount = report.StopCount,
            TotalIdleMinutes = totalIdleMinutes,
            TotalDrivingMinutes = totalDrivingMinutes,
            TotalIgnitionOnMinutes = totalDrivingMinutes + totalIdleMinutes,
            AverageSpeed = averageSpeed,
            MaxSpeed = maxSpeed,
            OverspeedIncidentCount = overspeedIncidentCount,
            DailyBreakdown = dailyBreakdown
        };

        return ApiResponse<VehicleStatsResponseDto>.Ok(stats, "Stats retrieved successfully.");
    }

    private static (DateTime from, DateTime to) ResolvePeriodRange(string period, DateTime vehicleCreatedAt)
    {
        var nowLocal = DateTime.UtcNow + SriLankaOffset;
        var todayLocalStart = nowLocal.Date;

        DateTime fromLocal;
        switch (period)
        {
            case "week":
                fromLocal = todayLocalStart.AddDays(-6); // last 7 days including today
                break;
            case "month":
                fromLocal = todayLocalStart.AddDays(-29); // last 30 days including today
                break;
            case "all":
                fromLocal = vehicleCreatedAt + SriLankaOffset;
                break;
            case "today":
            default:
                fromLocal = todayLocalStart;
                break;
        }

        // Convert local boundaries back to UTC for the actual DB query.
        var from = fromLocal - SriLankaOffset;
        var to = DateTime.UtcNow;
        return (from, to);
    }

    // Groups trips and stops by Sri Lanka LOCAL calendar day, not UTC --
    // a trip starting late at night local time would otherwise be
    // misattributed to the wrong day on the chart.
    private static List<DailyStatDto> BuildDailyBreakdown(
        List<TripSummaryDto> trips, List<StopSummaryDto> stops, DateTime from, DateTime to)
    {
        var fromLocalDate = (from + SriLankaOffset).Date;
        var toLocalDate = (to + SriLankaOffset).Date;

        var result = new List<DailyStatDto>();
        for (var day = fromLocalDate; day <= toLocalDate; day = day.AddDays(1))
        {
            var dayTrips = trips.Where(t => (t.StartTime + SriLankaOffset).Date == day).ToList();
            var dayStops = stops.Where(s => (s.StartTime + SriLankaOffset).Date == day).ToList();

            decimal dayDrivingMinutes = dayTrips.Sum(t => t.DurationMinutes);
            decimal dayIdleMinutes = dayStops.Sum(s => s.DurationMinutes);
            decimal dayAvgSpeed = dayDrivingMinutes > 0
                ? dayTrips.Sum(t => t.AvgSpeed * t.DurationMinutes) / dayDrivingMinutes
                : 0;

            result.Add(new DailyStatDto
            {
                Date = day,
                DistanceKm = dayTrips.Sum(t => t.DistanceKm),
                AverageSpeed = dayAvgSpeed,
                MaxSpeed = dayTrips.Count > 0 ? dayTrips.Max(t => t.MaxSpeed) : 0,
                TripCount = dayTrips.Count,
                StopCount = dayStops.Count,
                IgnitionOnMinutes = dayDrivingMinutes + dayIdleMinutes
            });
        }

        return result;
    }
}