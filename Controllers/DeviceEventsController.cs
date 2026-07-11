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
    /// NOTE: the DeviceEventFilter should be scoped to the caller's own vehicles/devices
    /// for non-staff. Add an ownership check in DeviceEventService if this endpoint is
    /// exposed to the customer app, otherwise a customer could filter to another's device.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DeviceEventFilter filter)
    {
        var response = await _deviceEventService.GetAsync(filter);

        return StatusCode(response.StatusCode, response);
    }
}