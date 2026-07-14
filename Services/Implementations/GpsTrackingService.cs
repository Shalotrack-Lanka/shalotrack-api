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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

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
        if (!filter.VehicleId.HasValue)
        {
            return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Fail(
                (int)HttpStatusCode.BadRequest,
                "VehicleId is required.",
                "A specific vehicleId must be provided to retrieve tracking history."
            );
        }

        if (!_currentUser.IsStaff)
        {
            var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(filter.VehicleId.Value);
            if (vehicle is null ||
                !string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal))
            {
                return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Fail(
                    (int)HttpStatusCode.NotFound,
                    "Vehicle not found.",
                    $"No vehicle exists with ID '{filter.VehicleId.Value}'."
                );
            }
        }

        if (filter.PageSize > 500) filter.PageSize = 500;

        var tracking = await _repository.GetAsync(filter);

        return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Ok(
            tracking,
            "GPS tracking records retrieved successfully."
        );
    }

    /// <summary>
    /// Computes trip/stop summary for a vehicle over a date range: start point, end
    /// point, distance, and counts of trips/stops. A "stop" is 5+ continuous minutes
    /// stationary. A "trip" additionally requires at least 100m of straight-line
    /// displacement (filters GPS jitter while parked from being misread as a trip).
    /// DistanceKm is the real route distance -- sum of point-to-point movement along
    /// the trip -- not the straight-line displacement used for the jitter filter.
    /// </summary>
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

        const decimal speedThresholdKmh = 2m;
        const double minTripDisplacementMeters = 100;
        var stopThreshold = TimeSpan.FromMinutes(5);

        var trips = new List<TripSummaryDto>();
        int stopCount = 0;

        TrackingPointRaw? tripStart = null;
        TrackingPointRaw? lastMovingPoint = null;
        TrackingPointRaw? previousPointInTrip = null;
        double tripDistanceMeters = 0;
        DateTime? stationarySince = null;
        bool stopAlreadyCounted = false;

        foreach (var p in points)
        {
            bool isMoving = p.Speed > speedThresholdKmh;

            // Trip bookkeeping: open a new trip, or accumulate real route distance
            // for every point (moving or a brief sub-threshold stop) while one is open.
            if (isMoving && tripStart is null)
            {
                tripStart = p;
                previousPointInTrip = p;
                tripDistanceMeters = 0;
            }
            else if (tripStart is not null && previousPointInTrip is not null)
            {
                tripDistanceMeters += HaversineMeters(
                    (double)previousPointInTrip.Latitude, (double)previousPointInTrip.Longitude,
                    (double)p.Latitude, (double)p.Longitude);
                previousPointInTrip = p;
            }

            if (isMoving)
            {
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
                        // Straight-line displacement decides whether this was a REAL
                        // trip (jitter has near-zero net displacement even if some
                        // noisy distance accumulated above).
                        double displacementMeters = HaversineMeters(
                            (double)tripStart.Latitude, (double)tripStart.Longitude,
                            (double)lastMovingPoint.Latitude, (double)lastMovingPoint.Longitude);

                        if (displacementMeters >= minTripDisplacementMeters)
                        {
                            trips.Add(BuildTripSummary(tripStart, lastMovingPoint, tripDistanceMeters, inProgress: false));
                        }

                        tripStart = null;
                        lastMovingPoint = null;
                        previousPointInTrip = null;
                        tripDistanceMeters = 0;
                    }
                }
            }
        }

        // Vehicle was still moving when the window ran out — close the trip as "in progress."
        if (tripStart is not null && lastMovingPoint is not null)
        {
            double displacementMeters = HaversineMeters(
                (double)tripStart.Latitude, (double)tripStart.Longitude,
                (double)lastMovingPoint.Latitude, (double)lastMovingPoint.Longitude);

            if (displacementMeters >= minTripDisplacementMeters)
            {
                trips.Add(BuildTripSummary(tripStart, lastMovingPoint, tripDistanceMeters, inProgress: true));
            }
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

    private static TripSummaryDto BuildTripSummary(TrackingPointRaw start, TrackingPointRaw end, double distanceMeters, bool inProgress)
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
            DistanceKm = (decimal)(distanceMeters / 1000.0),
            InProgress = inProgress
        };
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }
}