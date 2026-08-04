using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.Vehicle;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicleService;
    private readonly IRoadSnappingService _roadSnappingService;

    public VehiclesController(
        IVehicleService vehicleService,
        IRoadSnappingService roadSnappingService)
    {
        _vehicleService = vehicleService;
        _roadSnappingService = roadSnappingService;
    }

    /// <summary>
    /// Retrieve all vehicles. Staff only.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Dealer")]
    public async Task<IActionResult> GetAll()
    {
        var response = await _vehicleService.GetAllAsync();

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve a vehicle by ID. Ownership is enforced in the service
    /// (owner is resolved from the loaded vehicle's customer).
    /// </summary>
    [HttpGet("{vehicleId:guid}")]
    public async Task<IActionResult> GetById(Guid vehicleId)
    {
        var response = await _vehicleService.GetByIdAsync(vehicleId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve all vehicles belonging to a customer. Caller must own the customer.
    /// </summary>
    [HttpGet("customer/{customerId:guid}")]
    [OwnsCustomer]
    public async Task<IActionResult> GetByCustomer(Guid customerId)
    {
        var response = await _vehicleService.GetByCustomerAsync(customerId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Create a new vehicle. Ownership of dto.CustomerId is enforced in the service.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateVehicleDto dto)
    {
        var response = await _vehicleService.CreateAsync(dto);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Update a vehicle.
    /// </summary>
    [HttpPut("{vehicleId:guid}")]
    public async Task<IActionResult> Update(
        Guid vehicleId,
        [FromBody] UpdateVehicleDto dto)
    {
        var response = await _vehicleService.UpdateAsync(
            vehicleId,
            dto);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Delete a vehicle.
    /// </summary>
    [HttpDelete("{vehicleId:guid}")]
    public async Task<IActionResult> Delete(Guid vehicleId)
    {
        var response = await _vehicleService.DeleteAsync(vehicleId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Snaps a batch of raw GPS points onto the actual road network for this
    /// vehicle's live trail rendering. Ownership is enforced in the service
    /// (same pattern as GetById/Update/Delete above). Max 100 points per
    /// call (Google Roads API's own limit).
    /// </summary>
    [HttpPost("{vehicleId:guid}/snap-to-road")]
    public async Task<IActionResult> SnapToRoad(
        Guid vehicleId,
        [FromBody] SnapToRoadRequestDto request)
    {
        var response = await _roadSnappingService.SnapToRoadAsync(vehicleId, request);

        return StatusCode(response.StatusCode, response);
    }
}