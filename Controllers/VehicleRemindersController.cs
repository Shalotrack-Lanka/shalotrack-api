using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.VehicleReminder;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VehicleRemindersController : ControllerBase
{
    private readonly IVehicleReminderService _service;

    public VehicleRemindersController(IVehicleReminderService service)
    {
        _service = service;
    }

    [HttpGet("vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> GetForVehicle(Guid vehicleId)
    {
        var response = await _service.GetForVehicleAsync(vehicleId);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Create or replace the reminder of this type for the vehicle.</summary>
    [HttpPut("vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> Upsert(Guid vehicleId, [FromBody] UpsertVehicleReminderDto dto)
    {
        var response = await _service.UpsertAsync(vehicleId, dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{reminderId:guid}")]
    public async Task<IActionResult> Delete(Guid reminderId)
    {
        var response = await _service.DeleteAsync(reminderId);
        return StatusCode(response.StatusCode, response);
    }
}