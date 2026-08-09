using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.Alert;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;

    public AlertsController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    // NEW: vehicleId is optional -- GET /api/Alerts?vehicleId=... filters to
    // one vehicle; omitting it keeps the existing "all my vehicles" behavior.
    [HttpGet]
    public async Task<IActionResult> GetMyAlerts([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? vehicleId = null)
    {
        var response = await _alertService.GetMyAlertsAsync(page, pageSize, vehicleId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPatch("{alertId:long}/read")]
    public async Task<IActionResult> MarkAsRead(long alertId)
    {
        var response = await _alertService.MarkAsReadAsync(alertId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("register-token")]
    public async Task<IActionResult> RegisterToken([FromBody] RegisterFcmTokenDto dto)
    {
        var response = await _alertService.RegisterFcmTokenAsync(dto);
        return StatusCode(response.StatusCode, response);
    }
}