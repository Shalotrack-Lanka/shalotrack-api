using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;
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

    public InternalController(
        ICustomerService customerService,
        IVehicleService vehicleService,
        IGpsTrackingService gpsTrackingService)
    {
        _customerService = customerService;
        _vehicleService = vehicleService;
        _gpsTrackingService = gpsTrackingService;
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
    public async Task<IActionResult> GpsTrackingSync([FromQuery] GpsTrackingFilter filter, [FromQuery] string? imei)
    {
        // FIX: HTML datetime-local inputs parse as Kind=Unspecified, but Npgsql
        // requires Kind=Utc for "timestamp with time zone" columns — crashes
        // with a 500 the moment From/To are actually supplied.
        if (filter.From.HasValue) filter.From = DateTime.SpecifyKind(filter.From.Value, DateTimeKind.Utc);
        if (filter.To.HasValue) filter.To = DateTime.SpecifyKind(filter.To.Value, DateTimeKind.Utc);

        var vehiclesResponse = await _vehicleService.GetAllAsync();

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
            return StatusCode(400, ApiResponse<string>.Fail(400, "Vehicle ID or IMEI is required."));
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
}