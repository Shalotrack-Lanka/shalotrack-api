using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]   // service enforces per-vehicle ownership + staff-only GetAll
public class CurrentLocationsController : ControllerBase
{
    private readonly ICurrentLocationService _currentLocationService;

    public CurrentLocationsController(
        ICurrentLocationService currentLocationService)
    {
        _currentLocationService = currentLocationService;
    }

    /// <summary>
    /// Retrieves the latest location of all tracked vehicles. Staff only (enforced in service).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var response = await _currentLocationService.GetAllAsync();

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieves the latest location for a specific vehicle. Ownership enforced in service.
    /// </summary>
    [HttpGet("vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> GetByVehicle(Guid vehicleId)
    {
        var response = await _currentLocationService.GetByVehicleAsync(vehicleId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieves the latest location for a specific GPS device. Ownership enforced in service.
    /// </summary>
    [HttpGet("device/{deviceId:guid}")]
    public async Task<IActionResult> GetByDevice(Guid deviceId)
    {
        var response = await _currentLocationService.GetByDeviceAsync(deviceId);

        return StatusCode(response.StatusCode, response);
    }
}