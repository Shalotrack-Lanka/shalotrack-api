using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.Renewals;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

// Protected like every /api/internal route: AdminSyncKeyMiddleware matches on the path prefix
// and marks the request trusted (ICurrentUser.IsStaff). The service re-checks IsStaff anyway.
[ApiController]
[Route("api/internal/renewals")]
[AllowAnonymous]
public class InternalRenewalsController : ControllerBase
{
    private readonly IRenewalService _renewalService;

    public InternalRenewalsController(IRenewalService renewalService)
    {
        _renewalService = renewalService;
    }

    // ?status=PendingReview (default) | AwaitingSlip | Approved | Rejected | Cancelled | all
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var response = await _renewalService.GetForAdminAsync(status, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    // The slip is a customer's bank document: never cached, never sniffed, only ever served
    // through the admin portal's authenticated proxy.
    [HttpGet("{renewalRequestId:guid}/slip")]
    public async Task<IActionResult> GetSlip(Guid renewalRequestId, CancellationToken cancellationToken)
    {
        var slip = await _renewalService.GetSlipForAdminAsync(renewalRequestId, cancellationToken);
        if (slip is null) return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, no-store";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
        return File(slip.Value.Content, slip.Value.ContentType);
    }

    [HttpPost("{renewalRequestId:guid}/decision")]
    public async Task<IActionResult> Decide(Guid renewalRequestId, [FromBody] DecideRenewalDto dto)
    {
        var response = await _renewalService.DecideAsync(renewalRequestId, dto);
        return StatusCode(response.StatusCode, response);
    }
}