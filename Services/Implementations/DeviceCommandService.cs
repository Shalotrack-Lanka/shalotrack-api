using System.Collections.Concurrent;
using System.Net.Http.Json;
using ShaloTrack_API.DTOs.Command;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class DeviceCommandService : IDeviceCommandService
{
    private readonly IUnitOfWork _uow;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeviceCommandService> _logger;
    private readonly string _gatewayCommandApiUrl;

    // -----------------------------------------------------------------------
    // Command allowlist
    //
    // SECURITY: Only these commands can be sent by authenticated customers.
    //
    // Intentionally excluded:
    //   relay_off — engine cut. Safety-critical. Requires explicit speed-
    //               threshold spec and client approval before enabling.
    //   reset     — device reboot. Can cause GPS data gaps. Staff only.
    //   server    — changes server IP. Could redirect device to attacker.
    //   apn       — changes SIM APN. Could disconnect device permanently.
    //
    // relay_on is included — it RESTORES the engine relay (safe operation).
    // -----------------------------------------------------------------------
    private static readonly HashSet<string> CustomerAllowedCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "where",
        "status",
        "version",
        "imei",
        "params",
        "gprsset",
        "url",
        "position",
        "fence_query",
        "moving_query",
        "speed_query",
        "sos_query",
        "timer_query",
        "apn_query",
        "server_query",
        "relay_on",
        "sos_delete",
        "timer",
        "speed_alarm",
        "moving_alarm",
        "fence_circle",
        "sos_add",
        "batalm",
        "poweralm",
        "distance",
    };

    private const int MaxCommandsPerMinute = 10;
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _rateLimitStore = new();

    public DeviceCommandService(
        IUnitOfWork uow,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<DeviceCommandService> logger)
    {
        _uow = uow;
        _httpClient = httpClientFactory.CreateClient("GatewayCommandClient");
        _logger = logger;

        // Use DNS name — survives instance replacements without hardcoded IPs
        _gatewayCommandApiUrl = configuration["Gateway:CommandApiUrl"]
            ?? "http://gateway.shalotrack.internal:8001";
    }

    public async Task<ApiResponse<DeviceCommandResponseDto>> SendCommandAsync(
        Guid vehicleId,
        SendDeviceCommandDto dto,
        string? firebaseUid,
        bool isStaff)
    {
        // Step 1 — Validate command allowlist
        if (string.IsNullOrWhiteSpace(dto.Command))
            return ApiResponse<DeviceCommandResponseDto>.Fail(400, "Command is required.");

        if (!CustomerAllowedCommands.Contains(dto.Command))
            return ApiResponse<DeviceCommandResponseDto>.Fail(403,
                $"Command '{dto.Command}' is not permitted.");

        // Step 2 — Load vehicle with device assignment + IMEI
        var vehicle = await _uow.Vehicles.GetByIdAsync(vehicleId);
        if (vehicle is null || !vehicle.IsActive)
            return ApiResponse<DeviceCommandResponseDto>.Fail(404, "Vehicle not found.");

        // Step 3 — Ownership check (staff bypass)
        if (!isStaff)
        {
            if (string.IsNullOrEmpty(firebaseUid))
                return ApiResponse<DeviceCommandResponseDto>.Fail(401, "Unauthorized.");

            var customer = await _uow.Customers.GetByFirebaseUidAsync(firebaseUid);
            if (customer is null)
                return ApiResponse<DeviceCommandResponseDto>.Fail(404, "Customer not found.");

            if (vehicle.CustomerId != customer.CustomerId)
            {
                _logger.LogWarning(
                    "Customer {CustomerId} attempted command '{Command}' on vehicle {VehicleId} — DENIED",
                    customer.CustomerId, dto.Command, vehicleId);
                return ApiResponse<DeviceCommandResponseDto>.Fail(404, "Vehicle not found.");
            }

            var rateLimitKey = $"{customer.CustomerId}:{vehicleId}";
            if (!IsWithinRateLimit(rateLimitKey))
            {
                _logger.LogWarning(
                    "Rate limit exceeded for customer {CustomerId} on vehicle {VehicleId}",
                    customer.CustomerId, vehicleId);
                return ApiResponse<DeviceCommandResponseDto>.Fail(429,
                    $"Too many commands. Maximum {MaxCommandsPerMinute} per minute per vehicle.");
            }
        }

        // Step 4 — Get IMEI from active device assignment
        var assignment = vehicle.DeviceAssignments?
            .FirstOrDefault(a => a.Status == AssignmentStatus.Active);

        if (assignment?.Device is null)
            return ApiResponse<DeviceCommandResponseDto>.Fail(422,
                "No GPS device is currently assigned to this vehicle.");

        var imei = assignment.Device.ImeiNumber;
        if (string.IsNullOrWhiteSpace(imei))
            return ApiResponse<DeviceCommandResponseDto>.Fail(500, "Device IMEI is not set.");

        // Step 5 — Forward to gateway command API (internal VPC only)
        try
        {
            var payload = new
            {
                imei,
                command = dto.Command.ToLowerInvariant(),
                @params = dto.Params ?? new Dictionary<string, object>()
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_gatewayCommandApiUrl}/command", payload);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Command '{Command}' sent to IMEI {Imei} for vehicle {VehicleId}",
                    dto.Command, imei, vehicleId);

                return ApiResponse<DeviceCommandResponseDto>.Ok(new DeviceCommandResponseDto
                {
                    Success = true,
                    Message = $"Command '{dto.Command}' sent to device successfully.",
                    Command = dto.Command,
                    Imei = imei
                });
            }

            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                return ApiResponse<DeviceCommandResponseDto>.Fail(503,
                    "Device is currently offline. Command cannot be delivered.");

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Gateway rejected command: {StatusCode} {Body}",
                response.StatusCode, errorBody);

            return ApiResponse<DeviceCommandResponseDto>.Fail(
                (int)response.StatusCode,
                "Command could not be delivered to the device.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Gateway command API unreachable");
            return ApiResponse<DeviceCommandResponseDto>.Fail(503,
                "Gateway is temporarily unavailable. Please try again.");
        }
        catch (TaskCanceledException)
        {
            return ApiResponse<DeviceCommandResponseDto>.Fail(504,
                "Command timed out. The device may be temporarily unreachable.");
        }
    }

    public async Task<ApiResponse<bool>> IsDeviceOnlineAsync(string imei)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<GatewayDevicesResponseDto>(
                $"{_gatewayCommandApiUrl}/devices");
            var isOnline = response?.ConnectedDevices.Any(d => d.Imei == imei) ?? false;
            return ApiResponse<bool>.Ok(isOnline);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not check device online status from gateway");
            return ApiResponse<bool>.Ok(false);
        }
    }

    public async Task<ApiResponse<GatewayDevicesResponseDto>> GetConnectedDevicesAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<GatewayDevicesResponseDto>(
                $"{_gatewayCommandApiUrl}/devices");
            return ApiResponse<GatewayDevicesResponseDto>.Ok(
                response ?? new GatewayDevicesResponseDto());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch connected devices from gateway");
            return ApiResponse<GatewayDevicesResponseDto>.Fail(503,
                "Gateway is temporarily unavailable.");
        }
    }

    private static bool IsWithinRateLimit(string key)
    {
        var now = DateTime.UtcNow;
        var entry = _rateLimitStore.GetOrAdd(key, _ => (0, now));
        if ((now - entry.WindowStart).TotalSeconds >= 60)
            entry = (0, now);
        if (entry.Count >= MaxCommandsPerMinute)
            return false;
        _rateLimitStore[key] = (entry.Count + 1, entry.WindowStart);
        return true;
    }
}