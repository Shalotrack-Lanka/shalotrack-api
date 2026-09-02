using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.Geofence;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GeofencesController : ControllerBase
{
    private readonly IGeofenceService _geofenceService;

    public GeofencesController(IGeofenceService geofenceService)
    {
        _geofenceService = geofenceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyGeofences()
    {
        var response = await _geofenceService.GetMineAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost]
    public async Task<IActionResult> AddGeofence([FromBody] CreateGeofenceDto dto)
    {
        var response = await _geofenceService.CreateAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("{geofenceId:guid}")]
    public async Task<IActionResult> UpdateGeofence(Guid geofenceId, [FromBody] UpdateGeofenceDto dto)
    {
        var response = await _geofenceService.UpdateAsync(geofenceId, dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{geofenceId:guid}")]
    public async Task<IActionResult> DeleteGeofence(Guid geofenceId)
    {
        var response = await _geofenceService.DeleteAsync(geofenceId);
        return StatusCode(response.StatusCode, response);
    }
}