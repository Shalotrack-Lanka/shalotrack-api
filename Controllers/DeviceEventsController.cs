using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] DeviceEventFilter filter)
    {
        var response = await _deviceEventService.GetAsync(filter);

        return StatusCode(response.StatusCode, response);
    }
}