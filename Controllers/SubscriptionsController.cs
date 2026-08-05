using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.Subscription;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    [HttpPost]
    public async Task<IActionResult> RequestSubscription([FromBody] CreateSubscriptionDto dto)
    {
        var response = await _subscriptionService.RequestSubscriptionAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet]
    public async Task<IActionResult> GetMySubscriptions()
    {
        var response = await _subscriptionService.GetMySubscriptionsAsync();
        return StatusCode(response.StatusCode, response);
    }

    // Staff-only (enforced inside the service, same pattern as every other
    // staff-gated action in this API).
    [HttpPatch("{subscriptionId:guid}/confirm-payment")]
    public async Task<IActionResult> ConfirmPayment(Guid subscriptionId)
    {
        var response = await _subscriptionService.ConfirmPaymentAsync(subscriptionId);
        return StatusCode(response.StatusCode, response);
    }
}