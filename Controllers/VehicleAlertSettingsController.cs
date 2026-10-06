using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.VehicleAlertSettings;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VehicleAlertSettingsController : ControllerBase
{
    private readonly IVehicleAlertSettingsService _service;

    public VehicleAlertSettingsController(IVehicleAlertSettingsService service)
    {
        _service = service;
    }

    [HttpGet("vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> Get(Guid vehicleId)
    {
        var response = await _service.GetAsync(vehicleId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> Update(Guid vehicleId, [FromBody] UpdateVehicleAlertSettingsDto dto)
    {
        var response = await _service.UpdateAsync(vehicleId, dto);
        return StatusCode(response.StatusCode, response);
    }
}