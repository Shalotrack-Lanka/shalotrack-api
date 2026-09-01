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
    private readonly ITripArchivalService _tripArchivalService;
    private readonly ISetupShalotrackDeviceService _setupShalotrackDeviceService;
    private readonly IDeviceCommandService _deviceCommandService;

    public InternalController(
        ICustomerService customerService,
        IVehicleService vehicleService,
        IGpsTrackingService gpsTrackingService,
        ITripArchivalService tripArchivalService,
        ISetupShalotrackDeviceService setupShalotrackDeviceService,
        IDeviceCommandService deviceCommandService)
    {
        _customerService = customerService;
        _vehicleService = vehicleService;
        _gpsTrackingService = gpsTrackingService;
        _tripArchivalService = tripArchivalService;
        _setupShalotrackDeviceService = setupShalotrackDeviceService;
        _deviceCommandService = deviceCommandService;
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
        if (filter.From.HasValue) filter.From = DateTime.SpecifyKind(filter.From.Value, DateTimeKind.Utc);
        if (filter.To.HasValue) filter.To = DateTime.SpecifyKind(filter.To.Value, DateTimeKind.Utc);

        var vehiclesResponse = await _vehicleService.GetAllAsync();

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
            currentLocation = trackingResponse.Data?.FirstOrDefault(),
            trackingHistory = trackingResponse.Data,
        });
    }

    [HttpPost("setup-devices-sync")]
    public async Task<IActionResult> SetupDevicesSync([FromBody] SyncSetupShalotrackDeviceDto dto)
    {
        var response = await _setupShalotrackDeviceService.UpsertAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Get command history for a vehicle's GPS device.
    /// Protected by AdminSyncKeyMiddleware — X-Admin-Sync-Key header required.
    /// Used by the admin portal to display sent commands and device responses.
    /// </summary>
    [HttpGet("command-history")]
    public async Task<IActionResult> CommandHistorySync(
        [FromQuery] Guid vehicleId,
        [FromQuery] int limit = 20)
    {
        if (vehicleId == Guid.Empty)
            return StatusCode(400, ApiResponse<string>.Fail(400, "vehicleId is required."));

        // Staff bypass — isStaff=true skips ownership check
        var response = await _deviceCommandService.GetCommandHistoryAsync(
            vehicleId,
            firebaseUid: null,
            isStaff: true,
            limit: limit);

        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("archive-trip-test")]
    public async Task<IActionResult> ArchiveTripTest(
        [FromQuery] Guid deviceId,
        [FromQuery] Guid vehicleId,
        [FromQuery] DateTime tripEndTime)
    {
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