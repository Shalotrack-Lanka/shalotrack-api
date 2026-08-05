using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class TripArchivalService : ITripArchivalService
{
    private const decimal MovingSpeedThresholdKmh = 7m;
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
            return ignitionOnAlert.TriggeredAt;
        }

        _logger.LogWarning(
            "TripArchivalService: no prior IgnitionOn alert found for device {DeviceId} before {TripEnd} -- " +
            "falling back to a {Hours}h safety cap instead of pulling unbounded history.",
            deviceId, tripEndTime, FallbackMaxTripDuration.TotalHours);

        return tripEndTime - FallbackMaxTripDuration;
    }

    private static string BuildS3Key(Guid deviceId, DateTime tripStart, DateTime tripEnd)
    {
        const string format = "yyyyMMddTHHmmssZ";
        return $"archive/{deviceId}/{tripStart:yyyy}/{tripStart:MM}/{tripStart.ToString(format)}_{tripEnd.ToString(format)}.json";
    }
}