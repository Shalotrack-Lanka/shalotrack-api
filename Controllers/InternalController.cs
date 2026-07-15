using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

// Deliberately separate from CustomersController — this route is gated by
// AdminSyncKeyMiddleware instead of Firebase, so it must never share a
// controller with the customer-facing, [Authorize]-protected endpoints.
[ApiController]
[Route("api/internal")]
[AllowAnonymous]
public class InternalController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public InternalController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    /// <summary>
    /// Interim sync endpoint for the Laravel Admin portal. Gated by
    /// AdminSyncKeyMiddleware (X-Admin-Sync-Key header), not Firebase.
    /// TODO: replace with a Firebase service-account token and remove this route.
    /// </summary>
    [HttpGet("customers-sync")]
    public async Task<IActionResult> CustomersSync()
    {
        var response = await _customerService.GetAllAsync();
        return StatusCode(response.StatusCode, response);
    }
}