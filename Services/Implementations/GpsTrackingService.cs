using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class GpsTrackingService : IGpsTrackingService
{
    private readonly IGpsTrackingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;      // NEW — to resolve the vehicle's owner
    private readonly ICurrentUser _currentUser;    // NEW

    public GpsTrackingService(
        IGpsTrackingRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>> GetAsync(
        GpsTrackingFilter filter)
    {
        // FIX: VehicleId was optional. Omitting it returned every vehicle's trail in
        // the system — a full-fleet location-history leak. It's now required.
        if (!filter.VehicleId.HasValue)
        {
            return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Fail(
                (int)HttpStatusCode.BadRequest,
                "VehicleId is required.",
                "A specific vehicleId must be provided to retrieve tracking history."
            );
        }

        // FIX: no ownership check existed. Any authenticated user could pass any
        // vehicleId and read that vehicle's full movement trail.
        if (!_currentUser.IsStaff)
        {
            var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(filter.VehicleId.Value);
            if (vehicle is null ||
                !string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal))
            {
                // 404, not 403 — don't confirm the vehicle exists to a non-owner.
                return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Fail(
                    (int)HttpStatusCode.NotFound,
                    "Vehicle not found.",
                    $"No vehicle exists with ID '{filter.VehicleId.Value}'."
                );
            }
        }

        // Cap the page size server-side so a client can't request an unbounded trail.
        if (filter.PageSize > 500) filter.PageSize = 500;

        var tracking = await _repository.GetAsync(filter);

        return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Ok(
            tracking,
            "GPS tracking records retrieved successfully."
        );
    }

    public async Task<ApiResponse<TripsReportResponseDto>> GetTripsSummaryAsync(Guid vehicleId, DateTime from, DateTime to)
    {
        if (vehicleId == Guid.Empty)
        {
            return ApiResponse<TripsReportResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest,
                "VehicleId is required.",
                "A specific vehicleId must be provided."
            );
        }

        if (to <= from)
        {
            return ApiResponse<TripsReportResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest,
                "Invalid date range.",
                "'to' must be after 'from'."
            );
        }

        if (!_currentUser.IsStaff)
        {
            var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);
            if (vehicle is null ||
                !string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal))
            {
                return ApiResponse<TripsReportResponseDto>.Fail(
                    (int)HttpStatusCode.NotFound,
                    "Vehicle not found.",
                    $"No vehicle exists with ID '{vehicleId}'."
                );
            }
        }

        var points = await _repository.GetPointsForTripsAsync(vehicleId, from, to);

        const decimal speedThresholdKmh = 2m;              // ignores GPS jitter while parked
        var stopThreshold = TimeSpan.FromMinutes(5);        // matches the V5 device's own stopped-report interval

        var trips = new List<TripSummaryDto>();
        int stopCount = 0;

        TrackingPointRaw? tripStart = null;
        TrackingPointRaw? lastMovingPoint = null;
        DateTime? stationarySince = null;
        bool stopAlreadyCounted = false;

        foreach (var p in points)
        {
            bool isMoving = p.Speed > speedThresholdKmh;

            if (isMoving)
            {
                tripStart ??= p;
                lastMovingPoint = p;
                stationarySince = null;
                stopAlreadyCounted = false;
            }
            else
            {
                stationarySince ??= p.EventTime;
                var elapsedStationary = p.EventTime - stationarySince.Value;

                if (elapsedStationary >= stopThreshold && !stopAlreadyCounted)
                {
                    stopCount++;
                    stopAlreadyCounted = true;

                    if (tripStart is not null && lastMovingPoint is not null)
                    {
                        trips.Add(BuildTripSummary(tripStart, lastMovingPoint, inProgress: false));
                        tripStart = null;
                        lastMovingPoint = null;
                    }
                }
            }
        }

        // Vehicle was still moving when the window ran out — close the trip as "in progress."
        if (tripStart is not null && lastMovingPoint is not null)
        {
            trips.Add(BuildTripSummary(tripStart, lastMovingPoint, inProgress: true));
        }

        var report = new TripsReportResponseDto
        {
            VehicleId = vehicleId,
            From = from,
            To = to,
            TripCount = trips.Count,
            StopCount = stopCount,
            Trips = trips
        };

        return ApiResponse<TripsReportResponseDto>.Ok(report, "Trips summary retrieved successfully.");
    }

    private static TripSummaryDto BuildTripSummary(TrackingPointRaw start, TrackingPointRaw end, bool inProgress)
    {
        return new TripSummaryDto
        {
            StartTime = start.EventTime,
            EndTime = end.EventTime,
            StartLatitude = start.Latitude,
            StartLongitude = start.Longitude,
            EndLatitude = end.Latitude,
            EndLongitude = end.Longitude,
            DurationMinutes = (decimal)(end.EventTime - start.EventTime).TotalMinutes,
            InProgress = inProgress
        };
    }
}