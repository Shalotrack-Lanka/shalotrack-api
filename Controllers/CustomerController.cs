using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.Customer;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]                                   // every action requires a valid token
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// Retrieve all customers. Staff only.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Dealer")]
    public async Task<IActionResult> GetAll()
    {
        var response = await _customerService.GetAllAsync();

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve a customer by ID. Caller must own the record.
    /// </summary>
    [HttpGet("{customerId:guid}")]
    [OwnsCustomer]
    public async Task<IActionResult> GetById(Guid customerId)
    {
        var response = await _customerService.GetByIdAsync(customerId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Create a new customer. The token proves the verified Firebase account;
    /// the service binds the new record to that uid.
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
    /// Update an existing customer. Caller must own the record.
    /// </summary>
    [HttpPut("{customerId:guid}")]
    [OwnsCustomer]
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
    /// Deactivate a customer. Caller must own the record.
    /// </summary>
    [HttpDelete("{customerId:guid}")]
    [OwnsCustomer]
    public async Task<IActionResult> Deactivate(Guid customerId)
    {
        var response = await _customerService.DeactivateAsync(customerId);

        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Retrieve customer dashboard. Caller must own the record.
    /// </summary>
    [HttpGet("{customerId:guid}/dashboard")]
    [OwnsCustomer]
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