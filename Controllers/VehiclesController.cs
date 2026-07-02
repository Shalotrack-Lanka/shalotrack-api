using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.Vehicle;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicleService;

    public VehiclesController(
        IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    /// <summary>
    /// Retrieve all vehicles.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var response = await _vehicleService.GetAllAsync();

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve a vehicle by ID.
    /// </summary>
    [HttpGet("{vehicleId:guid}")]
    public async Task<IActionResult> GetById(Guid vehicleId)
    {
        var response = await _vehicleService.GetByIdAsync(vehicleId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve all vehicles belonging to a customer.
    /// </summary>
    [HttpGet("customer/{customerId:guid}")]
    public async Task<IActionResult> GetByCustomer(Guid customerId)
    {
        var response = await _vehicleService.GetByCustomerAsync(customerId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Create a new vehicle.
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
}