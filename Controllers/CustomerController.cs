using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.Customer;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// Retrieve all customers.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var response = await _customerService.GetAllAsync();

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve a customer by ID.
    /// </summary>
    [HttpGet("{customerId:guid}")]
    public async Task<IActionResult> GetById(Guid customerId)
    {
        var response = await _customerService.GetByIdAsync(customerId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Create a new customer.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCustomerDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _customerService.CreateAsync(dto);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Update an existing customer.
    /// </summary>
    [HttpPut("{customerId:guid}")]
    public async Task<IActionResult> Update(
        Guid customerId,
        [FromBody] UpdateCustomerDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _customerService.UpdateAsync(
            customerId,
            dto);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Deactivate a customer.
    /// </summary>
    [HttpDelete("{customerId:guid}")]
    public async Task<IActionResult> Deactivate(Guid customerId)
    {
        var response = await _customerService.DeactivateAsync(customerId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve customer dashboard.
    /// </summary>
    [HttpGet("{customerId:guid}/dashboard")]
    public async Task<IActionResult> GetDashboard(
        Guid customerId)
    {
        var response =
            await _customerService.GetDashboardAsync(customerId);

        return StatusCode(
            response.StatusCode,
            response);
    }
}