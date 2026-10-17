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
/// READ STRATEGY — S3 merge:
/// 1. Query Supabase for live/recent rows.
/// 2. Query S3 archive for the same window and merge.
/// 3. Deduplicate by EventTime.
/// 4. Run trip detection over the merged points.
///
/// Supabase and S3 data are merged regardless of whether archive
/// purging is running in dry-run mode or has removed database rows.
/// </summary>
public class GpsTrackingService : IGpsTrackingService
{
    private readonly IGpsTrackingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAmazonS3 _s3Client;
    private readonly string? _bucketName;
    private readonly IArchivedTripCache _archivedTripCache;
    private readonly ILogger<GpsTrackingService> _logger;
    private readonly ISubscriptionGateService _subscriptionGate;
    private const int MaxTripReportDays = 90;
    private const int MaxPageSize = 5000;
    private const int DefaultPageSize = 2000;
    private const string S3ArchivePrefix = "archive/";

    public GpsTrackingService(
        IGpsTrackingRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAmazonS3 s3Client,
        IConfiguration configuration,
        IArchivedTripCache archivedTripCache,
        ILogger<GpsTrackingService> logger,
        ISubscriptionGateService subscriptionGate)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _s3Client = s3Client;
        _archivedTripCache = archivedTripCache;
        _logger = logger;
        _subscriptionGate = subscriptionGate;
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
                "A specific vehicleId must be provided to retrieve tracking history.");
        }

        if (!_currentUser.IsStaff)
        {
            var vehicle = await _unitOfWork.Vehicles
                .GetByIdForOwnershipCheckAsync(filter.VehicleId.Value);

            bool isOwner = vehicle is not null &&
                string.Equals(
                    vehicle.Customer?.FirebaseUid,
                    _currentUser.FirebaseUid,
                    StringComparison.Ordinal);

            bool hasAcceptedShare = false;

            if (!isOwner && vehicle is not null)
            {
                var customer = await _unitOfWork.Customers
                    .GetByFirebaseUidAsync(_currentUser.FirebaseUid ?? string.Empty);

                if (customer is not null)
                {
                    var share = await _unitOfWork.VehicleShares
                        .GetByVehicleAndSharedWithAsync(
                            filter.VehicleId.Value,
                            customer.CustomerId);

                    hasAcceptedShare = share is not null &&
                        share.Status == VehicleShareStatus.Accepted;
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

        if (await _subscriptionGate.IsRenewalRequiredAsync(filter.VehicleId.Value))
        {
            return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Fail(
                (int)HttpStatusCode.PaymentRequired,
                "Subscription renewal required.",
                ISubscriptionGateService.RenewalRequiredCode);
        }

        // Validate pagination before calculating Skip().
        // Keep the page size bounded to avoid excessively large responses.
        if (filter.Page < 1)
        {
            filter.Page = 1;
        }

        if (filter.PageSize < 1)
        {
            filter.PageSize = DefaultPageSize;
        }

        if (filter.PageSize > MaxPageSize)
        {
            filter.PageSize = MaxPageSize;
        }

        if (filter.From.HasValue && filter.To.HasValue)
        {
            if (filter.To.Value <= filter.From.Value)
            {
                return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Fail(
                    (int)HttpStatusCode.BadRequest,
                    "Invalid date range.",
                    "'to' must be after 'from'.");
            }

            if ((filter.To.Value - filter.From.Value).TotalDays > MaxTripReportDays)
            {
                return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Fail(
                    (int)HttpStatusCode.BadRequest,
                    "Date range too large.",
                    $"Tracking history is limited to {MaxTripReportDays} days per request. Split longer ranges into multiple calls.");
            }

            var merged = await GetMergedPointsAsync(
                filter.VehicleId.Value,
                filter.From.Value,
                filter.To.Value);

            var mapped = merged.Points
                .Select(p => new GpsTrackingResponseDto
                {
                    TrackingId = 0,
                    DeviceId = merged.DeviceId ?? Guid.Empty,
                    ImeiNumber = merged.ImeiNumber,
                    VehicleId = filter.VehicleId.Value,
                    VehicleNumber = merged.VehicleNumber,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    Altitude = null,
                    Speed = p.Speed,
                    Heading = 0,
                    Satellites = 0,
                    GpsAccuracy = null,
                    EventTime = p.EventTime
                })
                .OrderByDescending(x => x.EventTime)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToList();

            _logger.LogInformation(
                "GpsTrackingService: Returned {ReturnedCount} tracking points " +
                "for vehicle {VehicleId}, page {Page}, page size {PageSize}.",
                mapped.Count,
                filter.VehicleId.Value,
                filter.Page,
                filter.PageSize);

            return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Ok(
                mapped,
                "GPS tracking records retrieved successfully.");
        }

        var tracking = await _repository.GetAsync(filter);

        return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Ok(
            tracking,
            "GPS tracking records retrieved successfully.");
    }

    public async Task<ApiResponse<TripsReportResponseDto>> GetTripsSummaryAsync(
        Guid vehicleId,
        DateTime from,
        DateTime to)
    {
        if (vehicleId == Guid.Empty)
        {
            return ApiResponse<TripsReportResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest,
                "VehicleId is required.",
                "A specific vehicleId must be provided.");
        }

        if (to <= from)
        {
            return ApiResponse<TripsReportResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest,
                "Invalid date range.",
                "'to' must be after 'from'.");
        }

        if ((to - from).TotalDays > MaxTripReportDays)
        {
            return ApiResponse<TripsReportResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest,
                "Date range too large.",
                $"Trip reports are limited to {MaxTripReportDays} days per request. Split longer ranges into multiple calls.");
        }

        if (!_currentUser.IsStaff)
        {
            var vehicle = await _unitOfWork.Vehicles
                .GetByIdForOwnershipCheckAsync(vehicleId);

            bool isOwner = vehicle is not null &&
                string.Equals(
                    vehicle.Customer?.FirebaseUid,
                    _currentUser.FirebaseUid,
                    StringComparison.Ordinal);

            bool hasAcceptedShare = false;

            if (!isOwner && vehicle is not null)
            {
                var customer = await _unitOfWork.Customers
                    .GetByFirebaseUidAsync(_currentUser.FirebaseUid ?? string.Empty);

                if (customer is not null)
                {
                    var share = await _unitOfWork.VehicleShares
                        .GetByVehicleAndSharedWithAsync(
                            vehicleId,
                            customer.CustomerId);

                    hasAcceptedShare = share is not null &&
                        share.Status == VehicleShareStatus.Accepted;
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

        if (await _subscriptionGate.IsRenewalRequiredAsync(vehicleId))
        {
            return ApiResponse<TripsReportResponseDto>.Fail(
                (int)HttpStatusCode.PaymentRequired,
                "Subscription renewal required.",
                ISubscriptionGateService.RenewalRequiredCode);
        }

        var merged = await GetMergedPointsAsync(vehicleId, from, to);

        return ApiResponse<TripsReportResponseDto>.Ok(
            ComputeTripsReport(vehicleId, from, to, merged.Points),
            "Trips summary retrieved successfully.");
    }

    public async Task<TripsReportResponseDto?> GetTripsReportForSystemAsync(
        Guid vehicleId,
        DateTime from,
        DateTime to)
    {
        if (vehicleId == Guid.Empty ||
            to <= from ||
            (to - from).TotalDays > MaxTripReportDays)
        {
            return null;
        }

        // Apply the same lapsed-subscription rule as the user-facing path.
        if (await _subscriptionGate.IsRenewalRequiredAsync(vehicleId))
        {
            return null;
        }

        var merged = await GetMergedPointsAsync(vehicleId, from, to);

        return ComputeTripsReport(vehicleId, from, to, merged.Points);
    }

    private sealed record MergedTrackingResult(
        List<TrackingPointRaw> Points,
        Guid? DeviceId,
        string VehicleNumber,
        string ImeiNumber);

    /// <summary>
    /// Fetches GPS points from Supabase and S3, merges and deduplicates them.
    /// </summary>
    private async Task<MergedTrackingResult> GetMergedPointsAsync(
        Guid vehicleId,
        DateTime from,
        DateTime to)
    {
        // Resolve vehicle metadata so demo fallback works regardless
        // of whether S3 archiving is configured.
        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);

        bool isDemo = vehicle?.IsDemoVehicle ?? false;
        string vehicleNumber = vehicle?.VehicleNumber ?? string.Empty;

        // Regular vehicles use active assignments.
        // Demo vehicles can fall back to the most recent assignment.
        var activeAssignment = vehicle?.DeviceAssignments?
            .FirstOrDefault(a => a.Status == AssignmentStatus.Active);

        if (activeAssignment is null && isDemo)
        {
            activeAssignment = vehicle!.DeviceAssignments?
                .OrderByDescending(a => a.AssignedAt)
                .FirstOrDefault();

            if (activeAssignment is not null)
            {
                _logger.LogInformation(
                    "GpsTrackingService: Demo vehicle {VehicleId} has no active " +
                    "assignment; falling back to most recent assignment " +
                    "(DeviceId: {DeviceId}).",
                    vehicleId,
                    activeAssignment.DeviceId);
            }
            else
            {
                _logger.LogWarning(
                    "GpsTrackingService: Demo vehicle {VehicleId} has no device " +
                    "assignments. GPS data will be empty.",
                    vehicleId);
            }
        }

        Guid? resolvedDeviceId = activeAssignment?.DeviceId;
        string imeiNumber = activeAssignment?.Device?.ImeiNumber ?? string.Empty;

        // Supabase query. Demo vehicles can include points regardless of
        // assignment state.
        var supabasePoints = await _repository.GetPointsForTripsAsync(
            vehicleId,
            from,
            to,
            isDemo);

        var points = new List<TrackingPointRaw>(supabasePoints);

        // Merge archived S3 points into the database results.
        if (!string.IsNullOrEmpty(_bucketName))
        {
            var s3Targets = new List<(Guid DeviceId, DateTime From, DateTime To)>();

            if (resolvedDeviceId.HasValue)
            {
                // Prevent archived points from appearing before a device was
                // assigned to a regular vehicle.
                var activeFrom =
                    !isDemo &&
                    activeAssignment is not null &&
                    activeAssignment.AssignedAt > from
                        ? activeAssignment.AssignedAt
                        : from;

                if (activeFrom < to)
                {
                    s3Targets.Add((
                        resolvedDeviceId.Value,
                        activeFrom,
                        to));
                }
            }

            // Include previously assigned devices only for their actual
            // assignment windows. Demo vehicles retain their full history.
            if (!isDemo && vehicle?.DeviceAssignments is not null)
            {
                foreach (var assignment in vehicle.DeviceAssignments.Where(a =>
                    a.Status == AssignmentStatus.Removed &&
                    a.RemovedAt.HasValue &&
                    a.DeviceId != resolvedDeviceId))
                {
                    var clippedFrom =
                        from > assignment.AssignedAt
                            ? from
                            : assignment.AssignedAt;

                    var clippedTo =
                        to < assignment.RemovedAt!.Value
                            ? to
                            : assignment.RemovedAt.Value;

                    if (clippedFrom < clippedTo)
                    {
                        s3Targets.Add((
                            assignment.DeviceId,
                            clippedFrom,
                            clippedTo));
                    }
                }
            }

            if (s3Targets.Count == 0)
            {
                _logger.LogWarning(
                    "GpsTrackingService: Could not resolve DeviceId for vehicle " +
                    "{VehicleId}; S3 merge skipped.",
                    vehicleId);
            }

            foreach (var target in s3Targets)
            {
                var s3Points = await ReadPointsFromS3Async(
                    target.DeviceId,
                    target.From,
                    target.To);

                if (s3Points.Count == 0)
                {
                    continue;
                }

                _logger.LogInformation(
                    "GpsTrackingService: S3 returned {S3Count} point(s) for " +
                    "device {DeviceId}. Supabase returned {DbCount} point(s). " +
                    "Merging.",
                    s3Points.Count,
                    target.DeviceId,
                    supabasePoints.Count);

                // Deduplicate points by EventTime across both data sources.
                var existingTimes = new HashSet<DateTime>(
                    points.Select(p => p.EventTime));

                foreach (var s3Point in s3Points)
                {
                    if (existingTimes.Add(s3Point.EventTime))
                    {
                        points.Add(s3Point);
                    }
                }

                points.Sort((a, b) => a.EventTime.CompareTo(b.EventTime));

                _logger.LogInformation(
                    "GpsTrackingService: Merged total {Total} point(s) " +
                    "for vehicle {VehicleId}.",
                    points.Count,
                    vehicleId);
            }
        }

        return new MergedTrackingResult(
            points,
            resolvedDeviceId,
            vehicleNumber,
            imeiNumber);
    }

    private async Task<List<TrackingPointRaw>> ReadPointsFromS3Async(
        Guid deviceId,
        DateTime from,
        DateTime to)
    {
        var allPoints = new List<TrackingPointRaw>();

        var months = new List<(int Year, int Month, string Prefix)>();
        var seenMonths = new HashSet<(int, int)>();

        var cursor = new DateTime(
            from.Year,
            from.Month,
            1,
            0,
            0,
            0,
            DateTimeKind.Utc);

        while (cursor <= to)
        {
            if (seenMonths.Add((cursor.Year, cursor.Month)))
            {
                months.Add((
                    cursor.Year,
                    cursor.Month,
                    $"{S3ArchivePrefix}{deviceId}/{cursor:yyyy}/{cursor:MM}/"));
            }

            cursor = cursor.AddMonths(1);
        }

        foreach (var (year, month, prefix) in months.OrderBy(m => m.Prefix))
        {
            if (_archivedTripCache.ShouldSkipMonth(deviceId, year, month))
            {
                continue;
            }

            List<S3Object> objects;

            try
            {
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = prefix
                };

                var listResponse = await _s3Client.ListObjectsV2Async(listRequest);

                // AWS SDK v4 may return null when no objects match.
                objects = listResponse.S3Objects ?? new List<S3Object>();
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "GpsTrackingService: S3 ListObjects failed for prefix {Prefix}.",
                    prefix);

                continue;
            }

            if (objects.Count == 0)
            {
                _archivedTripCache.MarkMonthEmpty(deviceId, year, month);
                continue;
            }

            var overlapping = new List<S3Object>();

            foreach (var obj in objects)
            {
                var fileName = obj.Key.Split('/').Last();

                if (!TryParseTripWindow(
                    fileName,
                    out var fileStart,
                    out var fileEnd))
                {
                    _logger.LogWarning(
                        "GpsTrackingService: Could not parse trip window from " +
                        "S3 key {Key}; skipping.",
                        obj.Key);

                    continue;
                }

                if (fileEnd < from || fileStart > to)
                {
                    continue;
                }

                overlapping.Add(obj);
            }

            if (overlapping.Count > 0)
            {
                var monthPoints = await DownloadAndParseObjectsAsync(
                    overlapping,
                    from,
                    to);

                allPoints.AddRange(monthPoints);
            }
        }

        allPoints.Sort((a, b) => a.EventTime.CompareTo(b.EventTime));

        return allPoints;
    }

    private const int S3DownloadConcurrency = 8;

    private async Task<List<TrackingPointRaw>> DownloadAndParseObjectsAsync(
        List<S3Object> objects,
        DateTime from,
        DateTime to)
    {
        var results =
            new System.Collections.Concurrent.ConcurrentBag<TrackingPointRaw>();

        using var gate = new SemaphoreSlim(S3DownloadConcurrency);

        var tasks = objects.Select(async obj =>
        {
            await gate.WaitAsync();

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

                foreach (var point in DeserialiseGeoJsonPoints(json, from, to))
                {
                    results.Add(point);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "GpsTrackingService: Failed to download or parse S3 object {Key}.",
                    obj.Key);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        return results.ToList();
    }

    private static bool TryParseTripWindow(
        string fileName,
        out DateTime start,
        out DateTime end)
    {
        start = default;
        end = default;

        var name = fileName.EndsWith(
            ".json",
            StringComparison.OrdinalIgnoreCase)
                ? fileName[..^5]
                : fileName;

        var parts = name.Split('_');

        if (parts.Length != 2)
        {
            return false;
        }

        const string format = "yyyyMMddTHHmmssZ";

        return DateTime.TryParseExact(
                   parts[0],
                   format,
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeUniversal |
                   System.Globalization.DateTimeStyles.AdjustToUniversal,
                   out start)
               &&
               DateTime.TryParseExact(
                   parts[1],
                   format,
                   System.Globalization.CultureInfo.InvariantCulture,
                   System.Globalization.DateTimeStyles.AssumeUniversal |
                   System.Globalization.DateTimeStyles.AdjustToUniversal,
                   out end);
    }

    private List<TrackingPointRaw> DeserialiseGeoJsonPoints(
        string json,
        DateTime from,
        DateTime to)
    {
        var points = new List<TrackingPointRaw>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("features", out var features))
            {
                return points;
            }

            foreach (var feature in features.EnumerateArray())
            {
                try
                {
                    var coords = feature
                        .GetProperty("geometry")
                        .GetProperty("coordinates");

                    var lon = coords[0].GetDouble();
                    var lat = coords[1].GetDouble();

                    var props = feature.GetProperty("properties");

                    if (!props.TryGetProperty("EventTime", out var eventTimeProp))
                    {
                        continue;
                    }

                    if (!DateTime.TryParse(
                        eventTimeProp.GetString(),
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal |
                        System.Globalization.DateTimeStyles.AdjustToUniversal,
                        out var eventTime))
                    {
                        continue;
                    }

                    if (eventTime < from || eventTime > to)
                    {
                        continue;
                    }

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
                    _logger.LogWarning(
                        ex,
                        "GpsTrackingService: Skipping malformed GeoJSON feature " +
                        "during S3 deserialisation.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "GpsTrackingService: Failed to parse GeoJSON from S3.");
        }

        return points;
    }

    private static TripsReportResponseDto ComputeTripsReport(
        Guid vehicleId,
        DateTime from,
        DateTime to,
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
                {
                    stops.Add(BuildStopSummary(
                        stopStartPoint,
                        lastStationaryPoint!,
                        inProgress: false));
                }

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
                        (double)previousPointInTrip.Latitude,
                        (double)previousPointInTrip.Longitude,
                        (double)p.Latitude,
                        (double)p.Longitude);

                    previousPointInTrip = p;
                }

                lastMovingPoint = p;

                if (p.Speed > tripMaxSpeed)
                {
                    tripMaxSpeed = p.Speed;
                }

                tripSpeedSum += p.Speed;
                tripSpeedPointCount++;
            }
            else
            {
                if (tripStart is not null && previousPointInTrip is not null)
                {
                    tripDistanceMeters += HaversineMeters(
                        (double)previousPointInTrip.Latitude,
                        (double)previousPointInTrip.Longitude,
                        (double)p.Latitude,
                        (double)p.Longitude);

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
                            (double)tripStart.Latitude,
                            (double)tripStart.Longitude,
                            (double)lastMovingPoint.Latitude,
                            (double)lastMovingPoint.Longitude);

                        if (displacement >= minTripDisplacementMeters)
                        {
                            decimal avg = tripSpeedPointCount > 0
                                ? tripSpeedSum / tripSpeedPointCount
                                : 0;

                            trips.Add(BuildTripSummary(
                                tripStart,
                                lastMovingPoint,
                                tripDistanceMeters,
                                tripMaxSpeed,
                                avg,
                                inProgress: false));
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

        if (tripStart is not null &&
            lastMovingPoint is not null &&
            lastStationaryPoint is not null)
        {
            var timeSinceLastPoint = to - lastStationaryPoint.EventTime;

            if (timeSinceLastPoint >= stopThreshold)
            {
                double displacement = HaversineMeters(
                    (double)tripStart.Latitude,
                    (double)tripStart.Longitude,
                    (double)lastMovingPoint.Latitude,
                    (double)lastMovingPoint.Longitude);

                if (displacement >= minTripDisplacementMeters)
                {
                    decimal avg = tripSpeedPointCount > 0
                        ? tripSpeedSum / tripSpeedPointCount
                        : 0;

                    trips.Add(BuildTripSummary(
                        tripStart,
                        lastMovingPoint,
                        tripDistanceMeters,
                        tripMaxSpeed,
                        avg,
                        inProgress: false));
                }

                tripStart = null;
                lastMovingPoint = null;
            }
        }

        if (tripStart is not null && lastMovingPoint is not null)
        {
            double displacement = HaversineMeters(
                (double)tripStart.Latitude,
                (double)tripStart.Longitude,
                (double)lastMovingPoint.Latitude,
                (double)lastMovingPoint.Longitude);

            if (displacement >= minTripDisplacementMeters)
            {
                decimal avg = tripSpeedPointCount > 0
                    ? tripSpeedSum / tripSpeedPointCount
                    : 0;

                trips.Add(BuildTripSummary(
                    tripStart,
                    lastMovingPoint,
                    tripDistanceMeters,
                    tripMaxSpeed,
                    avg,
                    inProgress: true));
            }
        }

        if (stopStartPoint is not null && stopAlreadyCounted)
        {
            stops.Add(BuildStopSummary(
                stopStartPoint,
                lastStationaryPoint!,
                inProgress: true));
        }

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
        TrackingPointRaw start,
        TrackingPointRaw end,
        double distanceMeters,
        decimal maxSpeed,
        decimal avgSpeed,
        bool inProgress) => new()
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
        TrackingPointRaw start,
        TrackingPointRaw end,
        bool inProgress) => new()
        {
            StartTime = start.EventTime,
            EndTime = end.EventTime,
            Latitude = start.Latitude,
            Longitude = start.Longitude,
            DurationMinutes = (decimal)(end.EventTime - start.EventTime).TotalMinutes,
            InProgress = inProgress
        };

    private static double HaversineMeters(
        double lat1,
        double lon1,
        double lat2,
        double lon2)
    {
        const double R = 6371000;

        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;

        double a =
            Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
            Math.Cos(lat1 * Math.PI / 180) *
            Math.Cos(lat2 * Math.PI / 180) *
            Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
