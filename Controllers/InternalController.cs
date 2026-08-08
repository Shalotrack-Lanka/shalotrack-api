using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;
using ShaloTrack_API.DTOs.SetupShalotrackDevice;
using System.Linq;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/internal")]
[AllowAnonymous]
public class InternalController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly IVehicleService _vehicleService;
    private readonly IGpsTrackingService _gpsTrackingService;
    private readonly ITripArchivalService _tripArchivalService; // NEW -- Phase 3a manual test only
    private readonly ISetupShalotrackDeviceService _setupShalotrackDeviceService; // NEW

    public InternalController(
        ICustomerService customerService,
        IVehicleService vehicleService,
        IGpsTrackingService gpsTrackingService,
        ITripArchivalService tripArchivalService, // NEW
        ISetupShalotrackDeviceService setupShalotrackDeviceService) // NEW
    {
        _customerService = customerService;
        _vehicleService = vehicleService;
        _gpsTrackingService = gpsTrackingService;
        _tripArchivalService = tripArchivalService; // NEW
        _setupShalotrackDeviceService = setupShalotrackDeviceService; // NEW
    }

    [HttpGet("customers-sync")]
    public async Task<IActionResult> CustomersSync()
    {
        var response = await _customerService.GetAllAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("vehicles-sync")]
    public async Task<IActionResult> VehiclesSync()
    {
        var response = await _vehicleService.GetAllAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("gps-tracking-sync")]
    public async Task<IActionResult> GpsTrackingSync(
        [FromQuery] GpsTrackingFilter filter,
        [FromQuery] string? imei,
        [FromQuery] string? vehicleNumber)
    {
        // FIX: HTML datetime-local inputs parse as Kind=Unspecified, but Npgsql
        // requires Kind=Utc for "timestamp with time zone" columns — crashes
        // with a 500 the moment From/To are actually supplied.
        if (filter.From.HasValue) filter.From = DateTime.SpecifyKind(filter.From.Value, DateTimeKind.Utc);
        if (filter.To.HasValue) filter.To = DateTime.SpecifyKind(filter.To.Value, DateTimeKind.Utc);

        var vehiclesResponse = await _vehicleService.GetAllAsync();

        // NEW: search by Vehicle Number — checked first since Admin's search
        // box now resolves plate numbers here instead of querying the
        // Vehicles table directly (Admin has no direct DB access to it).
        if (!string.IsNullOrWhiteSpace(vehicleNumber) && !filter.VehicleId.HasValue)
        {
            var matchedByNumber = vehiclesResponse.Data?.FirstOrDefault(v =>
                string.Equals(v.VehicleNumber, vehicleNumber, StringComparison.OrdinalIgnoreCase));

            if (matchedByNumber is null)
            {
                return StatusCode(404, ApiResponse<string>.Fail(
                    404, "Vehicle not found.", $"No vehicle with number '{vehicleNumber}' was found."));
            }
            filter.VehicleId = matchedByNumber.VehicleId;
        }

        if (!string.IsNullOrWhiteSpace(imei) && !filter.VehicleId.HasValue)
        {
            var matchedByImei = vehiclesResponse.Data?.FirstOrDefault(v => v.Imei == imei);
            if (matchedByImei is null)
            {
                return StatusCode(404, ApiResponse<string>.Fail(
                    404, "Device not found.", $"No vehicle with a linked device matching IMEI '{imei}' was found."));
            }
            filter.VehicleId = matchedByImei.VehicleId;
        }

        if (!filter.VehicleId.HasValue)
        {
            return StatusCode(400, ApiResponse<string>.Fail(400, "Vehicle Number or IMEI is required."));
        }

        var vehicle = vehiclesResponse.Data?.FirstOrDefault(v => v.VehicleId == filter.VehicleId.Value);
        var trackingResponse = await _gpsTrackingService.GetAsync(filter);

        return Ok(new
        {
            statusCode = 200,
            vehicle,
            currentLocation = trackingResponse.Data?.FirstOrDefault(), // most recent point — history is newest-first
            trackingHistory = trackingResponse.Data,
        });
    }

    // NEW -- Admin pushes its Setup Shalotrack Devices registry here (create
    // or update) so the mobile side knows about every physical device
    // ShaloTrack has set up, for activation purposes. Deliberately a
    // separate table from GpsDevices, not a merge — see chat/PR notes.
    [HttpPost("setup-devices-sync")]
    public async Task<IActionResult> SetupDevicesSync([FromBody] SyncSetupShalotrackDeviceDto dto)
    {
        var response = await _setupShalotrackDeviceService.UpsertAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    // NEW -- Phase 3a manual verification ONLY. Not a real feature endpoint.
    // Do not call this against a device with live gateway traffic -- see chat
    // history for why (WP JK 9931 vs WP CAD 9934). Same auth model as every
    // other action in this controller: protected by AdminSyncKeyMiddleware's
    // X-Admin-Sync-Key header check on the /api/internal prefix, nothing more --
    // treat it as reachable only via `curl localhost` from inside an SSM
    // session, never through the public ALB/Cloudflare path. Remove this action
    // once Phase 3b wires ArchiveTripAsync into the real listener trigger and
    // this manual path is no longer needed.
    [HttpGet("archive-trip-test")]
    public async Task<IActionResult> ArchiveTripTest(
        [FromQuery] Guid deviceId,
        [FromQuery] Guid vehicleId,
        [FromQuery] DateTime tripEndTime)
    {
        // Same Kind=Unspecified vs Kind=Utc fix as GpsTrackingSync above --
        // applied here up front rather than rediscovering it a second time.
        tripEndTime = DateTime.SpecifyKind(tripEndTime, DateTimeKind.Utc);

        var result = await _tripArchivalService.ArchiveTripAsync(deviceId, vehicleId, tripEndTime);

        return Ok(new
        {
            statusCode = result.Success ? 200 : 500,
            result.Success,
            result.S3Key,
            result.PointCount,
            result.ErrorMessage
        });
    }
}