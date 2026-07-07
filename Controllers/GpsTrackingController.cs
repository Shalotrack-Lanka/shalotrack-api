using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GpsTrackingController : ControllerBase
{
    private readonly IGpsTrackingService _gpsTrackingService;

    public GpsTrackingController(
        IGpsTrackingService gpsTrackingService)
    {
        _gpsTrackingService = gpsTrackingService;
    }

    /// <summary>
    /// Retrieves GPS tracking history.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] GpsTrackingFilter filter)
    {
        var response = await _gpsTrackingService.GetAsync(filter);

        return StatusCode(response.StatusCode, response);
    }
}