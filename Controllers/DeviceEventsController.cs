using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DeviceEventsController : ControllerBase
{
    private readonly IDeviceEventService _deviceEventService;

    public DeviceEventsController(
        IDeviceEventService deviceEventService)
    {
        _deviceEventService = deviceEventService;
    }

    /// <summary>
    /// Retrieve device events with optional filtering.
    /// FIXED: this previously had no ownership validation at all -- flagged
    /// here as a known risk but never actually addressed until a systematic
    /// security audit found it. DeviceEventService now requires non-staff
    /// callers to specify VehicleId and validates they own it.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DeviceEventFilter filter)
    {
        var response = await _deviceEventService.GetAsync(filter);

        return StatusCode(response.StatusCode, response);
    }
}