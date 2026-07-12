using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]   // was missing entirely — ownership + auth now enforced in the service
public class GpsTrackingController : ControllerBase
{
    private readonly IGpsTrackingService _gpsTrackingService;

    public GpsTrackingController(
        IGpsTrackingService gpsTrackingService)
    {
        _gpsTrackingService = gpsTrackingService;
    }

    /// <summary>
    /// Retrieves GPS tracking history for a single vehicle. VehicleId is required —
    /// the service enforces that the caller owns it (or is staff).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] GpsTrackingFilter filter)
    {
        var response = await _gpsTrackingService.GetAsync(filter);

        return StatusCode(response.StatusCode, response);
    }
}