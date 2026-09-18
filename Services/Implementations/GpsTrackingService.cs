using System.Net;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Serves GPS tracking history and trip summaries.
///
/// READ STRATEGY — S3 merge (Phase 3d):
///   1. Query Supabase for live/recent rows.
///   2. ALWAYS also query S3 archive for the same window and merge,
///      deduplicating by EventTime so rows present in both (DryRun=true)
///      are never double-counted. This fills the gaps left by real purges
///      (DryRun=false) without requiring Supabase to be fully empty first.
///   3. Run the identical trip-detection algorithm over the merged set.
///
/// This makes GetTripsSummaryAsync correct regardless of whether
/// GpsArchive:PurgeDryRun is true or false, and regardless of whether
/// only part of the requested window was purged.
/// </summary>
public class GpsTrackingService : IGpsTrackingService
{
    private readonly IGpsTrackingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAmazonS3 _s3Client;
    private readonly string? _bucketName;
    private readonly ILogger<GpsTrackingService> _logger;

    // PERFORMANCE: Hard cap on date range for trip queries.
    // 90 days = ~130,000 points worst-case on t3.micro — acceptable ceiling.
    // Android fetches in 30-day chunks so this is a safety net, not the norm.
    private const int MaxTripReportDays = 90;

    // S3 archive path prefix — must match TripArchivalService.BuildS3Key exactly.
    // Format: archive/{deviceId}/{year}/{month}/{startZ}_{endZ}.json
    private const string S3ArchivePrefix = "archive/";

    public GpsTrackingService(
        IGpsTrackingRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<GpsTrackingService> logger)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _s3Client = s3Client;
        _logger = logger;

        // Null-safe — if not configured, S3 fallback is silently skipped.
        // This keeps the service functional even when deployed without S3 config.
        _bucketName = configuration["GpsArchive:BucketName"];
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
            var vehicle = await _unitOfWork.Vehicles.GetByIdForOwnershipCheckAsync(filter.VehicleId.Value);
            bool isOwner = vehicle is not null &&
                string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal);

            bool hasAcceptedShare = false;
            if (!isOwner && vehicle is not null)
            {
                var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(_currentUser.FirebaseUid ?? string.Empty);
                if (customer is not null)
                {
                    var share = await _unitOfWork.VehicleShares.GetByVehicleAndSharedWithAsync(filter.VehicleId.Value, customer.CustomerId);
                    hasAcceptedShare = share is not null && share.Status == VehicleShareStatus.Accepted;
                }
            }

            bool isDemoVehicle = vehicle?.IsDemoVehicle ?? false;

            if (!isOwner && !hasAcceptedShare && !isDemoVehicle)
            {
                return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Fail(
                    (int)HttpStatusCode.NotFound,
                    "Vehicle not found.",
                    $"No vehicle exists with ID '{filter.VehicleId.Value}'.");
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
    /// Computes trip and stop reports for a vehicle over a date range.
    /// Falls back to S3 archive when Supabase has no rows for the window
    /// (i.e. GpsArchive:PurgeDryRun=false has deleted them after archival).
    /// </summary>
    public async Task<ApiResponse<TripsReportResponseDto>> GetTripsSummaryAsync(
        Guid vehicleId, DateTime from, DateTime to)
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

        if ((to - from).TotalDays > MaxTripReportDays)
        {
            return ApiResponse<TripsReportResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest,
                "Date range too large.",
                $"Trip reports are limited to {MaxTripReportDays} days per request. " +
                $"Split longer ranges into multiple calls."
            );
        }

        // ── Ownership check ──────────────────────────────────────────────────
        Guid? resolvedDeviceId = null;

        if (!_currentUser.IsStaff)
        {
            var vehicle = await _unitOfWork.Vehicles.GetByIdForOwnershipCheckAsync(vehicleId);
            bool isOwner = vehicle is not null &&
                string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal);

            bool hasAcceptedShare = false;
            if (!isOwner && vehicle is not null)
            {
                var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(_currentUser.FirebaseUid ?? string.Empty);
                if (customer is not null)
                {
                    var share = await _unitOfWork.VehicleShares.GetByVehicleAndSharedWithAsync(vehicleId, customer.CustomerId);
                    hasAcceptedShare = share is not null && share.Status == VehicleShareStatus.Accepted;
                }
            }

            bool isDemoVehicle = vehicle?.IsDemoVehicle ?? false;

            if (!isOwner && !hasAcceptedShare && !isDemoVehicle)
            {
                return ApiResponse<TripsReportResponseDto>.Fail(
                    (int)HttpStatusCode.NotFound,
                    "Vehicle not found.",
                    $"No vehicle exists with ID '{vehicleId}'.");
            }
        }

        // ── Step 1: Query Supabase ───────────────────────────────────────────
        var supabasePoints = await _repository.GetPointsForTripsAsync(vehicleId, from, to);

        // ── Step 2: Always merge S3 archive data (Phase 3d) ─────────────────
        // PurgeDryRun=false deletes specific trip windows from Supabase after
        // archiving them to S3. Those deleted windows leave gaps in Supabase —
        // older data outside the purged window still exists. A "fall back only
        // when Supabase is empty" approach misses these gaps entirely.
        //
        // The correct approach: ALWAYS fetch S3 archive data for the window and
        // merge it with whatever Supabase returned. Deduplication by EventTime
        // ensures no point is double-counted if PurgeDryRun=true (i.e. rows
        // exist in both Supabase and S3 simultaneously during dry-run mode).
        var points = new List<TrackingPointRaw>(supabasePoints);

        if (!string.IsNullOrEmpty(_bucketName))
        {
            // Resolve the active DeviceId for this vehicle — S3 archives are
            // keyed by DeviceId, not VehicleId.
            var assignment = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);
            var activeAssignment = assignment?.DeviceAssignments?
                .FirstOrDefault(a => a.Status == AssignmentStatus.Active);

            if (activeAssignment is not null)
            {
                resolvedDeviceId = activeAssignment.DeviceId;
                var s3Points = await ReadPointsFromS3Async(resolvedDeviceId.Value, from, to);

                if (s3Points.Count > 0)
                {
                    _logger.LogInformation(
                        "GpsTrackingService: S3 returned {S3Count} point(s) for device {DeviceId}. " +
                        "Supabase returned {DbCount} point(s). Merging.",
                        s3Points.Count, resolvedDeviceId.Value, supabasePoints.Count);

                    // Merge: add S3 points whose EventTime doesn't already exist
                    // in Supabase. Uses a HashSet for O(1) lookup — safe on t3.micro
                    // for up to ~130,000 points (the 90-day cap).
                    var existingTimes = new HashSet<DateTime>(
                        supabasePoints.Select(p => p.EventTime));

                    foreach (var s3Point in s3Points)
                    {
                        if (!existingTimes.Contains(s3Point.EventTime))
                            points.Add(s3Point);
                    }

                    // Re-sort after merge — S3 points may interleave with Supabase
                    points.Sort((a, b) => a.EventTime.CompareTo(b.EventTime));

                    _logger.LogInformation(
                        "GpsTrackingService: Merged total {Total} point(s) for vehicle {VehicleId}.",
                        points.Count, vehicleId);
                }
            }
            else
            {
                _logger.LogWarning(
                    "GpsTrackingService: Could not resolve active DeviceId for vehicle {VehicleId} — S3 merge skipped.",
                    vehicleId);
            }
        }

        // ── Step 3: Trip detection (identical algorithm regardless of source) ─
        return ApiResponse<TripsReportResponseDto>.Ok(
            ComputeTripsReport(vehicleId, from, to, points),
            "Trips summary retrieved successfully.");
    }

    // ── S3 Archive Reader ────────────────────────────────────────────────────

    /// <summary>
    /// Lists all archive files for the given device that overlap the [from, to]
    /// window, downloads each one, deserialises the GeoJSON features, and returns
    /// a flat chronologically-ordered list of TrackingPointRaw.
    ///
    /// S3 key format (must match TripArchivalService.BuildS3Key):
    ///   archive/{deviceId}/{year}/{month}/{startZ}_{endZ}.json
    ///
    /// The month-level prefix is enumerated for every calendar month in the
    /// requested window so no files are missed across month boundaries.
    /// </summary>
    private async Task<List<TrackingPointRaw>> ReadPointsFromS3Async(
        Guid deviceId, DateTime from, DateTime to)
    {
        var allPoints = new List<TrackingPointRaw>();

        // Build the set of year/month prefixes that overlap the window.
        // A 30-day window crossing a month boundary needs two prefixes.
        var prefixes = new HashSet<string>();
        var cursor = new DateTime(from.Year, from.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        while (cursor <= to)
        {
            prefixes.Add($"{S3ArchivePrefix}{deviceId}/{cursor:yyyy}/{cursor:MM}/");
            cursor = cursor.AddMonths(1);
        }

        foreach (var prefix in prefixes.OrderBy(p => p))
        {
            List<S3Object> objects;
            try
            {
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = prefix
                };
                var listResponse = await _s3Client.ListObjectsV2Async(listRequest);
                objects = listResponse.S3Objects;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "GpsTrackingService: S3 ListObjects failed for prefix {Prefix}.", prefix);
                continue;
            }

            foreach (var obj in objects)
            {
                // Parse the trip window from the filename so we can skip files
                // that don't overlap the requested range without downloading them.
                // Filename format: {startZ}_{endZ}.json
                // e.g. 20260915T112119Z_20260915T112126Z.json
                var fileName = obj.Key.Split('/').Last();
                if (!TryParseTripWindow(fileName, out var fileStart, out var fileEnd))
                {
                    _logger.LogWarning(
                        "GpsTrackingService: Could not parse trip window from S3 key {Key} — skipping.",
                        obj.Key);
                    continue;
                }

                // Skip files whose trip window doesn't overlap [from, to]
                if (fileEnd < from || fileStart > to) continue;

                try
                {
                    var getRequest = new GetObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = obj.Key
                    };

                    using var getResponse = await _s3Client.GetObjectAsync(getRequest);
                    using var reader = new StreamReader(getResponse.ResponseStream);
                    var json = await reader.ReadToEndAsync();

                    var points = DeserialiseGeoJsonPoints(json, from, to);
                    allPoints.AddRange(points);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "GpsTrackingService: Failed to download or parse S3 object {Key}.", obj.Key);
                }
            }
        }

        // Chronological order — identical to what GetPointsForTripsAsync returns
        allPoints.Sort((a, b) => a.EventTime.CompareTo(b.EventTime));
        return allPoints;
    }

    /// <summary>
    /// Parses the trip window from an archive filename.
    /// Format: 20260915T112119Z_20260915T112126Z.json
    /// </summary>
    private static bool TryParseTripWindow(string fileName, out DateTime start, out DateTime end)
    {
        start = default;
        end = default;

        // Strip .json extension
        var name = fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            ? fileName[..^5]
            : fileName;

        var parts = name.Split('_');
        if (parts.Length != 2) return false;

        const string fmt = "yyyyMMddTHHmmssZ";
        return DateTime.TryParseExact(parts[0], fmt,
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeUniversal |
                   System.Globalization.DateTimeStyles.AdjustToUniversal,
                   out start)
               && DateTime.TryParseExact(parts[1], fmt,
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeUniversal |
                   System.Globalization.DateTimeStyles.AdjustToUniversal,
                   out end);
    }

    /// <summary>
    /// Deserialises a GeoJSON FeatureCollection produced by TripArchivalService
    /// into TrackingPointRaw instances, filtered to [from, to].
    ///
    /// Expected structure:
    /// {
    ///   "type": "FeatureCollection",
    ///   "features": [
    ///     {
    ///       "type": "Feature",
    ///       "geometry": { "type": "Point", "coordinates": [lon, lat] },
    ///       "properties": { "EventTime": "...", "Speed": 42.5, "MovementStatus": true }
    ///     }
    ///   ]
    /// }
    /// </summary>
    private List<TrackingPointRaw> DeserialiseGeoJsonPoints(string json, DateTime from, DateTime to)
    {
        var points = new List<TrackingPointRaw>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("features", out var features)) return points;

            foreach (var feature in features.EnumerateArray())
            {
                try
                {
                    // Coordinates: [longitude, latitude] — GeoJSON spec order
                    var coords = feature
                        .GetProperty("geometry")
                        .GetProperty("coordinates");

                    var lon = coords[0].GetDouble();
                    var lat = coords[1].GetDouble();

                    var props = feature.GetProperty("properties");

                    if (!props.TryGetProperty("EventTime", out var eventTimeProp)) continue;
                    if (!DateTime.TryParse(eventTimeProp.GetString(),
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.AssumeUniversal |
                            System.Globalization.DateTimeStyles.AdjustToUniversal,
                            out var eventTime)) continue;

                    // Only include points inside the requested window
                    if (eventTime < from || eventTime > to) continue;

                    var speed = props.TryGetProperty("Speed", out var speedProp)
                        ? (decimal)speedProp.GetDouble()
                        : 0m;

                    points.Add(new TrackingPointRaw
                    {
                        EventTime = eventTime,
                        Latitude = (decimal)lat,
                        Longitude = (decimal)lon,
                        Speed = speed
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "GpsTrackingService: Skipping malformed GeoJSON feature during S3 deserialisation.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GpsTrackingService: Failed to parse GeoJSON from S3.");
        }

        return points;
    }

    // ── Trip detection algorithm ─────────────────────────────────────────────
    // Extracted into its own method so it runs identically over Supabase
    // points and S3-sourced points — single source of truth, no duplication.

    private static TripsReportResponseDto ComputeTripsReport(
        Guid vehicleId, DateTime from, DateTime to,
        List<TrackingPointRaw> points)
    {
        const decimal speedThresholdKmh = 2m;
        const double minTripDisplacementMeters = 100;
        var stopThreshold = TimeSpan.FromMinutes(5);

        var trips = new List<TripSummaryDto>();
        var stops = new List<StopSummaryDto>();
        int stopCount = 0;

        TrackingPointRaw? tripStart = null;
        TrackingPointRaw? lastMovingPoint = null;
        TrackingPointRaw? previousPointInTrip = null;
        double tripDistanceMeters = 0;
        decimal tripMaxSpeed = 0;
        decimal tripSpeedSum = 0;
        int tripSpeedPointCount = 0;

        DateTime? stationarySince = null;
        TrackingPointRaw? stopStartPoint = null;
        TrackingPointRaw? lastStationaryPoint = null;
        bool stopAlreadyCounted = false;

        foreach (var p in points)
        {
            bool isMoving = p.Speed > speedThresholdKmh;

            if (isMoving)
            {
                if (stopStartPoint is not null && stopAlreadyCounted)
                    stops.Add(BuildStopSummary(stopStartPoint, lastStationaryPoint!, inProgress: false));

                stopStartPoint = null;
                lastStationaryPoint = null;
                stationarySince = null;
                stopAlreadyCounted = false;

                if (tripStart is null)
                {
                    tripStart = p;
                    previousPointInTrip = p;
                    tripDistanceMeters = 0;
                    tripMaxSpeed = 0;
                    tripSpeedSum = 0;
                    tripSpeedPointCount = 0;
                }
                else if (previousPointInTrip is not null)
                {
                    tripDistanceMeters += HaversineMeters(
                        (double)previousPointInTrip.Latitude, (double)previousPointInTrip.Longitude,
                        (double)p.Latitude, (double)p.Longitude);
                    previousPointInTrip = p;
                }

                lastMovingPoint = p;
                if (p.Speed > tripMaxSpeed) tripMaxSpeed = p.Speed;
                tripSpeedSum += p.Speed;
                tripSpeedPointCount++;
            }
            else
            {
                if (tripStart is not null && previousPointInTrip is not null)
                {
                    tripDistanceMeters += HaversineMeters(
                        (double)previousPointInTrip.Latitude, (double)previousPointInTrip.Longitude,
                        (double)p.Latitude, (double)p.Longitude);
                    previousPointInTrip = p;
                    tripSpeedSum += p.Speed;
                    tripSpeedPointCount++;
                }

                stopStartPoint ??= p;
                lastStationaryPoint = p;
                stationarySince ??= p.EventTime;
                var elapsedStationary = p.EventTime - stationarySince.Value;

                if (elapsedStationary >= stopThreshold && !stopAlreadyCounted)
                {
                    stopCount++;
                    stopAlreadyCounted = true;

                    if (tripStart is not null && lastMovingPoint is not null)
                    {
                        double displacement = HaversineMeters(
                            (double)tripStart.Latitude, (double)tripStart.Longitude,
                            (double)lastMovingPoint.Latitude, (double)lastMovingPoint.Longitude);

                        if (displacement >= minTripDisplacementMeters)
                        {
                            decimal avg = tripSpeedPointCount > 0 ? tripSpeedSum / tripSpeedPointCount : 0;
                            trips.Add(BuildTripSummary(tripStart, lastMovingPoint,
                                tripDistanceMeters, tripMaxSpeed, avg, inProgress: false));
                        }

                        tripStart = null;
                        lastMovingPoint = null;
                        previousPointInTrip = null;
                        tripDistanceMeters = 0;
                        tripMaxSpeed = 0;
                        tripSpeedSum = 0;
                        tripSpeedPointCount = 0;
                    }
                }
            }
        }

        // Ignition-off gap fix — device goes silent after engine off
        if (tripStart is not null && lastMovingPoint is not null && lastStationaryPoint is not null)
        {
            var timeSinceLastPoint = to - lastStationaryPoint.EventTime;
            if (timeSinceLastPoint >= stopThreshold)
            {
                double displacement = HaversineMeters(
                    (double)tripStart.Latitude, (double)tripStart.Longitude,
                    (double)lastMovingPoint.Latitude, (double)lastMovingPoint.Longitude);

                if (displacement >= minTripDisplacementMeters)
                {
                    decimal avg = tripSpeedPointCount > 0 ? tripSpeedSum / tripSpeedPointCount : 0;
                    trips.Add(BuildTripSummary(tripStart, lastMovingPoint,
                        tripDistanceMeters, tripMaxSpeed, avg, inProgress: false));
                }

                tripStart = null;
                lastMovingPoint = null;
            }
        }

        // Still moving at query boundary
        if (tripStart is not null && lastMovingPoint is not null)
        {
            double displacement = HaversineMeters(
                (double)tripStart.Latitude, (double)tripStart.Longitude,
                (double)lastMovingPoint.Latitude, (double)lastMovingPoint.Longitude);

            if (displacement >= minTripDisplacementMeters)
            {
                decimal avg = tripSpeedPointCount > 0 ? tripSpeedSum / tripSpeedPointCount : 0;
                trips.Add(BuildTripSummary(tripStart, lastMovingPoint,
                    tripDistanceMeters, tripMaxSpeed, avg, inProgress: true));
            }
        }

        // Stop still in progress at query boundary
        if (stopStartPoint is not null && stopAlreadyCounted)
            stops.Add(BuildStopSummary(stopStartPoint, lastStationaryPoint!, inProgress: true));

        return new TripsReportResponseDto
        {
            VehicleId = vehicleId,
            From = from,
            To = to,
            TripCount = trips.Count,
            StopCount = stopCount,
            Trips = trips,
            Stops = stops
        };
    }

    private static TripSummaryDto BuildTripSummary(
        TrackingPointRaw start, TrackingPointRaw end, double distanceMeters,
        decimal maxSpeed, decimal avgSpeed, bool inProgress) => new()
        {
            StartTime = start.EventTime,
            EndTime = end.EventTime,
            StartLatitude = start.Latitude,
            StartLongitude = start.Longitude,
            EndLatitude = end.Latitude,
            EndLongitude = end.Longitude,
            DurationMinutes = (decimal)(end.EventTime - start.EventTime).TotalMinutes,
            DistanceKm = (decimal)(distanceMeters / 1000.0),
            MaxSpeed = maxSpeed,
            AvgSpeed = avgSpeed,
            InProgress = inProgress
        };

    private static StopSummaryDto BuildStopSummary(
        TrackingPointRaw start, TrackingPointRaw end, bool inProgress) => new()
        {
            StartTime = start.EventTime,
            EndTime = end.EventTime,
            Latitude = start.Latitude,
            Longitude = start.Longitude,
            DurationMinutes = (decimal)(end.EventTime - start.EventTime).TotalMinutes,
            InProgress = inProgress
        };

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                 * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}