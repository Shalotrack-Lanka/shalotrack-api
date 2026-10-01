using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShaloTrack_API.DTOs.Renewals;
using ShaloTrack_API.Extensions;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

/// <summary>
/// Customer-facing device renewal. Every action is scoped to the caller's own vehicles inside the
/// service; nothing here can touch another customer's request.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RenewalsController : ControllerBase
{
    private readonly IRenewalService _renewalService;

    public RenewalsController(IRenewalService renewalService)
    {
        _renewalService = renewalService;
    }

    [HttpPost]
    [EnableRateLimiting(RateLimitingExtensions.Policies.AuthSensitive)]
    public async Task<IActionResult> Create([FromBody] CreateRenewalDto dto)
    {
        var response = await _renewalService.CreateAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    // Hard cap just above the 2 MB slip limit (multipart overhead); the service re-checks the real size.
    [HttpPost("{renewalRequestId:guid}/slip")]
    [RequestSizeLimit(3_000_000)]
    [EnableRateLimiting(RateLimitingExtensions.Policies.AuthSensitive)]
    public async Task<IActionResult> UploadSlip(Guid renewalRequestId, IFormFile file)
    {
        var response = await _renewalService.UploadSlipAsync(renewalRequestId, file);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var response = await _renewalService.GetMineAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpPatch("{renewalRequestId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid renewalRequestId)
    {
        var response = await _renewalService.CancelAsync(renewalRequestId);
        return StatusCode(response.StatusCode, response);
    }
}