using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.DeviceAssignment;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DeviceAssignmentsController : ControllerBase
{
    private readonly IDeviceAssignmentService _deviceAssignmentService;

    public DeviceAssignmentsController(
        IDeviceAssignmentService deviceAssignmentService)
    {
        _deviceAssignmentService = deviceAssignmentService;
    }

    /// <summary>
    /// Retrieve all device assignments. Staff only.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Dealer")]
    public async Task<IActionResult> GetAll()
    {
        var response = await _deviceAssignmentService.GetAllAsync();

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve a device assignment by ID.
    /// </summary>
    [HttpGet("{assignmentId:guid}")]
    public async Task<IActionResult> GetById(Guid assignmentId)
    {
        var response =
            await _deviceAssignmentService.GetByIdAsync(assignmentId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve assignment history for a vehicle.
    /// </summary>
    [HttpGet("vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> GetVehicleHistory(Guid vehicleId)
    {
        var response =
            await _deviceAssignmentService.GetHistoryByVehicleAsync(vehicleId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve assignment history for a GPS device.
    /// </summary>
    [HttpGet("device/{deviceId:guid}")]
    public async Task<IActionResult> GetDeviceHistory(Guid deviceId)
    {
        var response =
            await _deviceAssignmentService.GetHistoryByDeviceAsync(deviceId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Assign a GPS device to a vehicle.
    /// </summary>
    [HttpPost("assign")]
    public async Task<IActionResult> Assign(
        [FromBody] CreateDeviceAssignmentDto dto)
    {
        var response =
            await _deviceAssignmentService.AssignAsync(dto);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Unassign a GPS device from a vehicle.
    /// </summary>
    [HttpPatch("{assignmentId:guid}/unassign")]
    public async Task<IActionResult> Unassign(Guid assignmentId)
    {
        var response =
            await _deviceAssignmentService.UnassignAsync(assignmentId);

        return StatusCode(response.StatusCode, response);
    }
}