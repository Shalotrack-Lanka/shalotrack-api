using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShaloTrack_API.DTOs.Account;
using ShaloTrack_API.Extensions;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

/// <summary>
/// The signed-in customer's own data rights. No id appears in any route: the API always acts on the
/// customer resolved from the token.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly IAccountDataService _service;
    private readonly IAccountDeletionService _deletion;

    public AccountController(IAccountDataService service, IAccountDeletionService deletion)
    {
        _service = service;
        _deletion = deletion;
    }

    /// <summary>Download my data. Heavy (about 15 queries), so tightly rate-limited.</summary>
    [HttpGet("export")]
    [EnableRateLimiting(RateLimitingExtensions.Policies.AccountExport)]
    public async Task<IActionResult> Export()
    {
        Response.Headers.CacheControl = "no-store";
        var response = await _service.ExportAsync();
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Where the account stands: is deletion scheduled, and for when. Cheap; used at every sign-in.</summary>
    [HttpGet("deletion")]
    public async Task<IActionResult> DeletionStatus()
    {
        Response.Headers.CacheControl = "no-store";
        var response = await _deletion.GetStatusAsync();
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>
    /// Delete my account: locks it now and erases it permanently in 30 days unless cancelled.
    /// Needs {"confirm":"DELETE"} and a sign-in from the last 10 minutes.
    /// </summary>
    [HttpPost("delete")]
    [EnableRateLimiting(RateLimitingExtensions.Policies.AccountDelete)]
    public async Task<IActionResult> RequestDeletion([FromBody] DeleteAccountRequestDto dto)
    {
        Response.Headers.CacheControl = "no-store";
        var response = await _deletion.RequestAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    /// <summary>Withdraws a pending deletion (only possible before the permanent erasure).</summary>
    [HttpPost("deletion/cancel")]
    [EnableRateLimiting(RateLimitingExtensions.Policies.AccountDelete)]
    public async Task<IActionResult> CancelDeletion()
    {
        Response.Headers.CacheControl = "no-store";
        var response = await _deletion.CancelAsync();
        return StatusCode(response.StatusCode, response);
    }
}