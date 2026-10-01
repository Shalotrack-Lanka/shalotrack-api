using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.DeviceHealth;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

// Protected the same way as every other /api/internal route: AdminSyncKeyMiddleware matches on the
// path prefix, so this needs no extra wiring. Read-only.
[ApiController]
[Route("api/internal/devices")]
[AllowAnonymous]
public class InternalDeviceHealthController : ControllerBase
{
    private readonly IDeviceHealthService _deviceHealthService;

    public InternalDeviceHealthController(IDeviceHealthService deviceHealthService)
    {
        _deviceHealthService = deviceHealthService;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var health = await _deviceHealthService.GetFleetHealthAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<DeviceHealthDto>>.Ok(health, "Device health retrieved successfully."));
    }
}