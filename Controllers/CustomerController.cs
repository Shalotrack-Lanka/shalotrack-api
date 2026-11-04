using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.Customer;
using ShaloTrack_API.Extensions;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;
    private readonly IPhoneChangeService _phoneChangeService;

    public CustomersController(ICustomerService customerService, IPhoneChangeService phoneChangeService)
    {
        _customerService = customerService;
        _phoneChangeService = phoneChangeService;
    }

    /// <summary>
    /// Retrieve the logged-in caller's own customer profile.
    /// No customerId in the route — resolved from the token, so any authenticated
    /// customer (not just staff) can safely call this.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var response = await _customerService.GetMyProfileAsync();
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Step 1 of changing the sign-in phone number: text a one-time code to the NEW number.
    /// Needs a sign-in from the last 10 minutes (403 REAUTH_REQUIRED otherwise). The caller is always the
    /// signed-in customer (resolved from the token); there is no customerId to tamper with.
    /// </summary>
    [HttpPost("me/phone-change/request")]
    [EnableRateLimiting(RateLimitingExtensions.Policies.PhoneChangeRequest)]
    public async Task<IActionResult> RequestPhoneChange([FromBody] RequestPhoneChangeDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var response = await _phoneChangeService.RequestAsync(dto, ip);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Step 2: the code that arrived on the new number. On success the number changes on the customer record and on
    /// the Firebase login, other sessions are signed out, and the old number is told.
    /// </summary>
    [HttpPost("me/phone-change/verify")]
    [EnableRateLimiting(RateLimitingExtensions.Policies.PhoneChangeVerify)]
    public async Task<IActionResult> VerifyPhoneChange([FromBody] VerifyPhoneChangeDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _phoneChangeService.VerifyAsync(dto);
        return StatusCode(response.StatusCode, response);
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
    /// Create a new customer profile.
    /// Tighter rate limit (auth_sensitive) — 20 req/60s per IP — because
    /// this is the highest-value abuse target: spamming account creation
    /// with burner Firebase tokens.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitingExtensions.Policies.AuthSensitive)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto)
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

        var response = await _customerService.UpdateAsync(customerId, dto);
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
    public async Task<IActionResult> GetDashboard(Guid customerId)
    {
        var response = await _customerService.GetDashboardAsync(customerId);
        return StatusCode(response.StatusCode, response);
    }
}