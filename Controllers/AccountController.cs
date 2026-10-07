using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    public AccountController(IAccountDataService service)
    {
        _service = service;
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
}