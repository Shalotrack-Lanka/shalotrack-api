using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class TripArchivalService : ITripArchivalService
{
    // Matches the Python gateway's tuned movement threshold (tracking_service.py) --
    // duplicated here deliberately, since the two are different languages/repos and
    // can't share a literal constant. If that threshold is ever retuned, this needs
    // updating too -- nothing enforces the two staying in sync.
    private const decimal MovingSpeedThresholdKmh = 7m;

    // Defensive cap for when no valid prior IgnitionOn can be resolved (first-ever
    // trip, alert history lost before the Phase 2 fix shipped, or the only
    // candidate IgnitionOn was already closed by an intervening IgnitionOff --
    // see ResolveTripStartAsync). 24h is a safety ceiling, not a real trip
    // boundary in this fallback case.
    private static readonly TimeSpan FallbackMaxTripDuration = TimeSpan.FromHours(24);

    private readonly IGpsTrackingRepository _gpsTrackingRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<TripArchivalService> _logger;

    public TripArchivalService(
        IGpsTrackingRepository gpsTrackingRepository,
        IUnitOfWork unitOfWork,
        IAmazonS3 s3Client,
        IConfiguration configuration,
        ILogger<TripArchivalService> logger)
    {
        _gpsTrackingRepository = gpsTrackingRepository;

        // Alerts goes through IUnitOfWork -- it's actually wired in there.
        // GpsTrackings is NOT wired into IUnitOfWork, so that one is injected
        // directly instead. This split looks inconsistent; it is deliberate --
        // see the Phase 2/3a postmortems for what happens when that's assumed
        // without checking (IUnitOfWork.DeviceStatuses was declared but never
        // wired either, and stayed broken until something finally called it).
        _unitOfWork = unitOfWork;

        _s3Client = s3Client;
        _logger = logger;

        _bucketName = configuration["GpsArchive:BucketName"]
            ?? throw new InvalidOperationException(
                "GpsArchive:BucketName is not configured. Set via the " +
                "GpsArchive__BucketName environment variable.");
    }

    public async Task<TripArchivalResult> ArchiveTripAsync(
        Guid deviceId,
        Guid vehicleId,
        DateTime tripEndTime,
        CancellationToken cancellationToken = default)
    {
        var tripStart = await ResolveTripStartAsync(deviceId, tripEndTime);

        var points = await _gpsTrackingRepository.GetPointsForTripsAsync(vehicleId, tripStart, tripEndTime);

        if (points.Count == 0)
        {
            _logger.LogInformation(
                "TripArchivalService: no GpsTrackings points for device {DeviceId} in [{From}, {To}] -- nothing to archive.",
                deviceId, tripStart, tripEndTime);
            return new TripArchivalResult(true, null, 0, null);
        }

        var features = points.Select(p => new
        {
            type = "Feature",
            geometry = new
            {
                type = "Point",
                // GeoJSON coordinate order is [longitude, latitude] -- checked
                // deliberately, not assumed. Confirmed correct against real
                // Colombo-area coordinates during Phase 3a testing.
                coordinates = new[] { (double)p.Longitude, (double)p.Latitude }
            },
            properties = new
            {
                EventTime = p.EventTime,
                Speed = p.Speed,
                MovementStatus = p.Speed > MovingSpeedThresholdKmh
            }
        }).ToArray();

        var archiveDocument = new
        {
            type = "FeatureCollection",
            features,
            tripMetadata = new
            {
                DeviceId = deviceId,
                VehicleId = vehicleId,
                TripStart = tripStart,
                TripEnd = tripEndTime,
                PointCount = points.Count
            }
        };

        var json = JsonSerializer.Serialize(archiveDocument, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        var s3Key = BuildS3Key(deviceId, tripStart, tripEndTime);

        try
        {
            await _s3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key,
                ContentBody = json,
                ContentType = "application/geo+json"
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "TripArchivalService: S3 write failed for device {DeviceId}, key {Key}.",
                deviceId, s3Key);
            return new TripArchivalResult(false, null, points.Count, ex.Message);
        }

        _logger.LogInformation(
            "TripArchivalService: archived {Count} point(s) for device {DeviceId} to {Key}.",
            points.Count, deviceId, s3Key);

        return new TripArchivalResult(true, s3Key, points.Count, null);
    }

    private async Task<DateTime> ResolveTripStartAsync(Guid deviceId, DateTime tripEndTime)
    {
        var ignitionOnAlert = await _unitOfWork.Alerts.GetMostRecentByDeviceAndTypeAsync(
            deviceId, AlertType.IgnitionOn, tripEndTime);

        if (ignitionOnAlert is not null)
        {
            var candidateAge = tripEndTime - ignitionOnAlert.TriggeredAt;

            // FIX #2 (post re-test on WP CAD 9934): the intervening-IgnitionOff
            // check alone isn't enough. A device that goes permanently dark
            // (dead battery, lost SIM, physically disconnected) without ever
            // reporting an IgnitionOff looks IDENTICAL to a legitimately still-
            // open trip from this query's point of view -- "no intervening
            // close-out" is true in both cases. This age ceiling is the real
            // primary guard; the intervening-alert check below is an
            // additional guard on top of it, not a substitute for it.
            if (candidateAge <= FallbackMaxTripDuration)
            {
                // FIX #1 (post Phase 3a manual test on WP CAD 9934): a candidate
                // IgnitionOn is only valid as this trip's start if nothing closed
                // it in between. Without this, a stale IgnitionOn gets paired
                // with today's IgnitionOff, producing a bogus multi-day "trip".
                var hasInterveningIgnitionOff = await _unitOfWork.Alerts.ExistsByDeviceAndTypeBetweenAsync(
                    deviceId, AlertType.IgnitionOff, ignitionOnAlert.TriggeredAt, tripEndTime);

                if (!hasInterveningIgnitionOff)
                {
                    return ignitionOnAlert.TriggeredAt;
                }

                _logger.LogWarning(
                    "TripArchivalService: candidate IgnitionOn at {IgnitionOn} for device {DeviceId} was already " +
                    "closed by an intervening IgnitionOff before {TripEnd} -- falling back to the {Hours}h safety " +
                    "cap instead of reaching back past it.",
                    ignitionOnAlert.TriggeredAt, deviceId, tripEndTime, FallbackMaxTripDuration.TotalHours);
            }
            else
            {
                _logger.LogWarning(
                    "TripArchivalService: candidate IgnitionOn at {IgnitionOn} for device {DeviceId} is {AgeHours:F1}h " +
                    "old -- beyond the {CapHours}h safety cap regardless of whether an IgnitionOff exists in between " +
                    "(device may have gone dark without ever reporting one). Falling back to the safety cap.",
                    ignitionOnAlert.TriggeredAt, deviceId, candidateAge.TotalHours, FallbackMaxTripDuration.TotalHours);
            }
        }
        else
        {
            _logger.LogWarning(
                "TripArchivalService: no prior IgnitionOn alert found for device {DeviceId} before {TripEnd} -- " +
                "falling back to a {Hours}h safety cap instead of pulling unbounded history.",
                deviceId, tripEndTime, FallbackMaxTripDuration.TotalHours);
        }

        return tripEndTime - FallbackMaxTripDuration;
    }

    private static string BuildS3Key(Guid deviceId, DateTime tripStart, DateTime tripEnd)
    {
        const string format = "yyyyMMddTHHmmssZ";
        return $"archive/{deviceId}/{tripStart:yyyy}/{tripStart:MM}/{tripStart.ToString(format)}_{tripEnd.ToString(format)}.json";
    }
}