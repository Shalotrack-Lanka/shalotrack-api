using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.GpsDevice;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GpsDevicesController : ControllerBase
{
    private readonly IGpsDeviceService _gpsDeviceService;

    public GpsDevicesController(
        IGpsDeviceService gpsDeviceService)
    {
        _gpsDeviceService = gpsDeviceService;
    }

    /// <summary>
    /// Retrieve all GPS devices. Staff only — this is the hardware registry.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Dealer")]
    public async Task<IActionResult> GetAll()
    {
        var response = await _gpsDeviceService.GetAllAsync();

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve a GPS device by ID.
    /// </summary>
    [HttpGet("{deviceId:guid}")]
    public async Task<IActionResult> GetById(Guid deviceId)
    {
        var response = await _gpsDeviceService.GetByIdAsync(deviceId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Create a GPS device. Staff only — devices are provisioned by the business.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Dealer")]
    public async Task<IActionResult> Create(
        [FromBody] CreateGpsDeviceDto dto)
    {
        var response = await _gpsDeviceService.CreateAsync(dto);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Update a GPS device. Staff only.
    /// </summary>
    [HttpPut("{deviceId:guid}")]
    [Authorize(Roles = "Admin,Dealer")]
    public async Task<IActionResult> Update(
        Guid deviceId,
        [FromBody] UpdateGpsDeviceDto dto)
    {
        var response = await _gpsDeviceService.UpdateAsync(
            deviceId,
            dto);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Delete a GPS device. Staff only.
    /// </summary>
    [HttpDelete("{deviceId:guid}")]
    [Authorize(Roles = "Admin,Dealer")]
    public async Task<IActionResult> Delete(Guid deviceId)
    {
        var response = await _gpsDeviceService.DeleteAsync(deviceId);

        return StatusCode(response.StatusCode, response);
    }
}