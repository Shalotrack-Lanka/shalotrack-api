using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VehicleStatsController : ControllerBase
{
    private readonly IVehicleStatsService _vehicleStatsService;

    public VehicleStatsController(IVehicleStatsService vehicleStatsService)
    {
        _vehicleStatsService = vehicleStatsService;
    }

    // GET /api/VehicleStats/{vehicleId}?period=today|week|month|all
    [HttpGet("{vehicleId:guid}")]
    public async Task<IActionResult> GetStats(Guid vehicleId, [FromQuery] string? period)
    {
        var response = await _vehicleStatsService.GetStatsAsync(vehicleId, period);
        return StatusCode(response.StatusCode, response);
    }
}