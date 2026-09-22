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

    public GpsTrackingController(IGpsTrackingService gpsTrackingService)
    {
        _gpsTrackingService = gpsTrackingService;
    }

    /// <summary>
    /// Retrieves GPS tracking history for a single vehicle. VehicleId is required —
    /// the service enforces that the caller owns it (or is staff).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GpsTrackingFilter filter)
    {
        // Npgsql 6+ modern-timestamp mode requires DateTimeKind.Utc for all
        // timestamptz column comparisons.  ASP.NET Core model-binding produces
        // DateTimeKind.Unspecified from query-string values — Npgsql rejects those
        // at runtime with an InvalidOperationException.  Normalise here so callers
        // never need to append a 'Z' suffix.
        if (filter.From.HasValue)
            filter.From = DateTime.SpecifyKind(filter.From.Value, DateTimeKind.Utc);
        if (filter.To.HasValue)
            filter.To = DateTime.SpecifyKind(filter.To.Value, DateTimeKind.Utc);

        var response = await _gpsTrackingService.GetAsync(filter);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Computes trip/stop summary for a vehicle over a date range — start point,
    /// end point, and count of trips/stops. A "stop" is 5+ continuous minutes stationary.
    /// </summary>
    [HttpGet("trips")]
    public async Task<IActionResult> GetTrips(
        [FromQuery] Guid vehicleId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        // Same Npgsql 6+ requirement: DateTimeKind.Utc is mandatory for timestamptz.
        // The fleet portal and Android app both send bare ISO-8601 strings without a
        // timezone suffix, so we normalise at the controller boundary.
        from = DateTime.SpecifyKind(from, DateTimeKind.Utc);
        to = DateTime.SpecifyKind(to, DateTimeKind.Utc);

        var response = await _gpsTrackingService.GetTripsSummaryAsync(vehicleId, from, to);
        return StatusCode(response.StatusCode, response);
    }
}