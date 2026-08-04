using System.Net;
using System.Text.Json;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.Vehicle;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class RoadSnappingService : IRoadSnappingService
{
    private const int MaxPointsPerRequest = 100; // Google Roads API's own limit

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<RoadSnappingService> _logger;

    public RoadSnappingService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<RoadSnappingService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _httpClient = httpClientFactory.CreateClient("GoogleRoadsApi");
        _logger = logger;

        _apiKey = configuration["GoogleMaps:RoadsApiKey"]
            ?? throw new InvalidOperationException(
                "GoogleMaps:RoadsApiKey is not configured. Must be injected via the " +
                "GoogleMaps__RoadsApiKey environment variable, sourced from AWS SSM -- " +
                "same pattern as Firebase:ServiceAccountJson. Use a DIFFERENT key than " +
                "the Android app's Maps SDK key: that one is Android-app-restricted and " +
                "will reject these server-side calls, and it must never be reused here " +
                "regardless -- this key never reaches the Android app at all.");
    }

    public async Task<ApiResponse<IReadOnlyList<SnappedPointDto>>> SnapToRoadAsync(
        Guid vehicleId,
        SnapToRoadRequestDto request)
    {
        // Same ownership-check pattern as VehicleService.GetByIdAsync --
        // resource-id routes are checked in the service layer, not via
        // [OwnsCustomer] (that attribute is for {customerId} routes only).
        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);

        if (vehicle is null)
        {
            return ApiResponse<IReadOnlyList<SnappedPointDto>>.Fail(
                (int)HttpStatusCode.NotFound,
                "Vehicle not found.",
                $"No vehicle exists with ID '{vehicleId}'.");
        }

        if (!_currentUser.IsStaff &&
            !string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal))
        {
            return ApiResponse<IReadOnlyList<SnappedPointDto>>.Fail(
                (int)HttpStatusCode.NotFound,
                "Vehicle not found.",
                $"No vehicle exists with ID '{vehicleId}'.");
        }

        if (request.Points is null || request.Points.Count == 0)
        {
            return ApiResponse<IReadOnlyList<SnappedPointDto>>.Fail(
                (int)HttpStatusCode.BadRequest,
                "No points provided.",
                "The 'points' array must contain at least one point.");
        }

        if (request.Points.Count > MaxPointsPerRequest)
        {
            return ApiResponse<IReadOnlyList<SnappedPointDto>>.Fail(
                (int)HttpStatusCode.BadRequest,
                $"Too many points (max {MaxPointsPerRequest} per request).",
                $"Received {request.Points.Count} points, Google Roads API allows at most {MaxPointsPerRequest} per call.");
        }

        var path = string.Join("|", request.Points.Select(p =>
            $"{p.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"{p.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}"));

        var url = $"https://roads.googleapis.com/v1/snapToRoads?path={Uri.EscapeDataString(path)}&interpolate=true&key={_apiKey}";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Roads API request threw for vehicle {VehicleId}", vehicleId);
            return ApiResponse<IReadOnlyList<SnappedPointDto>>.Fail(
                (int)HttpStatusCode.BadGateway,
                "Could not reach the road-snapping service.",
                "Network error calling Google Roads API.");
        }

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // FIX (per this project's own repeated lesson this session):
            // log the actual response body, not just the status code --
            // a bare code has already caused two separate re-diagnosis
            // round-trips elsewhere in this project for exactly this
            // reason.
            _logger.LogWarning(
                "Roads API call failed for vehicle {VehicleId}, status {StatusCode}, body: {Body}",
                vehicleId, (int)response.StatusCode, body);

            return ApiResponse<IReadOnlyList<SnappedPointDto>>.Fail(
                (int)HttpStatusCode.BadGateway,
                "Road-snapping service returned an error.",
                $"Google Roads API responded with status {(int)response.StatusCode}.");
        }

        GoogleRoadsApiResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<GoogleRoadsApiResponse>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Roads API response for vehicle {VehicleId}: {Body}", vehicleId, body);
            return ApiResponse<IReadOnlyList<SnappedPointDto>>.Fail(
                (int)HttpStatusCode.BadGateway,
                "Road-snapping service returned an unexpected response.",
                "Could not parse Google Roads API response.");
        }

        var snappedPoints = (parsed?.SnappedPoints ?? new List<GoogleSnappedPoint>())
            .Select(sp => new SnappedPointDto
            {
                Latitude = sp.Location.Latitude,
                Longitude = sp.Location.Longitude,
                OriginalIndex = sp.OriginalIndex
            })
            .ToList();

        // Google can legitimately return fewer snapped points than
        // requested (points far from any known road get dropped) -- that's
        // not treated as a failure, per the original spec: return what
        // Google did successfully snap.
        return ApiResponse<IReadOnlyList<SnappedPointDto>>.Ok(
            snappedPoints,
            "Points snapped successfully.");
    }

    // Shapes matching Google's actual documented Roads API response --
    // internal to this service, not exposed to Android (SnappedPointDto is
    // the public contract).
    private class GoogleRoadsApiResponse
    {
        public List<GoogleSnappedPoint>? SnappedPoints { get; set; }
    }

    private class GoogleSnappedPoint
    {
        public GoogleLocation Location { get; set; } = new();
        public int? OriginalIndex { get; set; }
        public string? PlaceId { get; set; }
    }

    private class GoogleLocation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}