using System.Net;
using ShaloTrack_API.Auth;
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

    public VehicleStatsService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IGpsTrackingService gpsTrackingService)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _gpsTrackingService = gpsTrackingService;
    }

    public async Task<ApiResponse<VehicleStatsResponseDto>> GetStatsAsync(Guid vehicleId)
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

        // "All-time" -- from the vehicle's own creation date to now, rather
        // than an arbitrary fixed lookback window.
        var from = vehicle.CreatedAt;
        var to = DateTime.UtcNow;

        var tripsResult = await _gpsTrackingService.GetTripsSummaryAsync(vehicleId, from, to);
        if (tripsResult.Data is null)
        {
            return ApiResponse<VehicleStatsResponseDto>.Fail(
                tripsResult.StatusCode, tripsResult.Message, "Could not compute trip data for stats.");
        }

        var report = tripsResult.Data;

        decimal totalDistanceKm = report.Trips.Sum(t => t.DistanceKm);
        decimal totalIdleMinutes = report.Stops.Sum(s => s.DurationMinutes);
        decimal maxSpeed = report.Trips.Count > 0 ? report.Trips.Max(t => t.MaxSpeed) : 0;

        // Duration-weighted average -- a 2-minute trip and a 2-hour trip
        // shouldn't count equally toward the overall average speed.
        decimal totalDurationMinutes = report.Trips.Sum(t => t.DurationMinutes);
        decimal averageSpeed = totalDurationMinutes > 0
            ? report.Trips.Sum(t => t.AvgSpeed * t.DurationMinutes) / totalDurationMinutes
            : 0;

        int overspeedIncidentCount = await _unitOfWork.Alerts.CountByVehicleAndTypeAsync(
            vehicleId, AlertType.Overspeed, from, to);

        var stats = new VehicleStatsResponseDto
        {
            VehicleId = vehicleId,
            PeriodFrom = from,
            PeriodTo = to,
            TotalDistanceKm = totalDistanceKm,
            TotalTripCount = report.TripCount,
            TotalIdleMinutes = totalIdleMinutes,
            AverageSpeed = averageSpeed,
            MaxSpeed = maxSpeed,
            OverspeedIncidentCount = overspeedIncidentCount
        };

        return ApiResponse<VehicleStatsResponseDto>.Ok(stats, "Stats retrieved successfully.");
    }
}