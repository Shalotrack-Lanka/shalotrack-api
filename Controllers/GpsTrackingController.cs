using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
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
    /// NOTE: like DeviceEvents, this filter should be scoped to the caller's own vehicles
    /// for non-staff. Trip history is sensitive location data — add an ownership check in
    /// GpsTrackingService before exposing this to the customer app.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] GpsTrackingFilter filter)
    {
        var response = await _gpsTrackingService.GetAsync(filter);

        return StatusCode(response.StatusCode, response);
    }
}