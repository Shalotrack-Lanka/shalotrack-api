using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShaloTrack_API.DTOs.Alert;
using ShaloTrack_API.Extensions;
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

    [HttpGet]
    public async Task<IActionResult> GetMyAlerts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? vehicleId = null)
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

    /// <summary>
    /// NEW -- report generation feature ("Alert Report" card).
    /// </summary>
    [HttpGet("report")]
    public async Task<IActionResult> GetAlertReport(
        [FromQuery] Guid vehicleId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var response = await _alertService.GetAlertReportAsync(vehicleId, from, to);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("register-token")]
    [EnableRateLimiting(RateLimitingExtensions.Policies.AuthSensitive)]
    public async Task<IActionResult> RegisterToken([FromBody] RegisterFcmTokenDto dto)
    {
        var response = await _alertService.RegisterFcmTokenAsync(dto);
        return StatusCode(response.StatusCode, response);
    }
}