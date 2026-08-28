using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.Command;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/vehicles/{vehicleId:guid}/commands")]
[Authorize]
public class DeviceCommandsController : ControllerBase
{
    private readonly IDeviceCommandService _commandService;
    private readonly ICurrentUser _currentUser;

    public DeviceCommandsController(
        IDeviceCommandService commandService,
        ICurrentUser currentUser)
    {
        _commandService = commandService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Send a command to the GPS device assigned to a vehicle.
    /// Customer must own the vehicle. Commands validated against allowlist.
    /// Engine cut (relay_off) excluded. Rate limited to 10/min per vehicle.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SendCommand(
        Guid vehicleId,
        [FromBody] SendDeviceCommandDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _commandService.SendCommandAsync(
            vehicleId,
            dto,
            _currentUser.FirebaseUid,
            _currentUser.IsStaff);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Get all devices currently connected to the gateway. Staff only.
    /// </summary>
    [HttpGet("/api/gateway/devices")]
    [Authorize(Roles = "Admin,Dealer")]
    public async Task<IActionResult> GetConnectedDevices()
    {
        var response = await _commandService.GetConnectedDevicesAsync();
        return StatusCode(response.StatusCode, response);
    }
}