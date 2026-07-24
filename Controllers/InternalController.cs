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
    private readonly IGpsDeviceService _gpsDeviceService;

    public InternalController(
        ICustomerService customerService,
        IVehicleService vehicleService,
        IGpsTrackingService gpsTrackingService,
        IGpsDeviceService gpsDeviceService)
    {
        _customerService = customerService;
        _vehicleService = vehicleService;
        _gpsTrackingService = gpsTrackingService;
        _gpsDeviceService = gpsDeviceService;
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
        if (!string.IsNullOrWhiteSpace(imei) && filter.DeviceId is null)
        {
            var devicesResponse = await _gpsDeviceService.GetAllAsync();
            var matched = devicesResponse.Data?.FirstOrDefault(d => d.ImeiNumber == imei);

            if (matched is null)
            {
                return StatusCode(404, ApiResponse<string>.Fail(
                    404, "Device not found.", $"No device exists with IMEI '{imei}'."));
            }

            filter.DeviceId = matched.DeviceId;
        }

        var response = await _gpsTrackingService.GetAsync(filter);
        return StatusCode(response.StatusCode, response);
    }
}