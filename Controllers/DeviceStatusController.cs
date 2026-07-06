using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeviceStatusController : ControllerBase
{
    private readonly IDeviceStatusService _deviceStatusService;

    public DeviceStatusController(
        IDeviceStatusService deviceStatusService)
    {
        _deviceStatusService = deviceStatusService;
    }

    /// <summary>
    /// Retrieves the status of all GPS devices.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var response = await _deviceStatusService.GetAllAsync();

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieves the status of a GPS device.
    /// </summary>
    [HttpGet("device/{deviceId:guid}")]
    public async Task<IActionResult> GetByDevice(Guid deviceId)
    {
        var response = await _deviceStatusService.GetByDeviceAsync(deviceId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieves the device status assigned to a vehicle.
    /// </summary>
    [HttpGet("vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> GetByVehicle(Guid vehicleId)
    {
        var response = await _deviceStatusService.GetByVehicleAsync(vehicleId);

        return StatusCode(response.StatusCode, response);
    }
}