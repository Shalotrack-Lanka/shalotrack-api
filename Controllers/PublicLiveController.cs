using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShaloTrack_API.Extensions;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

/// <summary>
/// The only anonymous customer-data endpoint. The token in the path is the credential. It is
/// read-only, returns one vehicle's plate + position (+ trail since the link was created), is
/// rate-limited per link, and is never cached.
/// </summary>
[ApiController]
[Route("api/public/live")]
[AllowAnonymous]
public class PublicLiveController : ControllerBase
{
    private readonly ILiveShareService _service;

    public PublicLiveController(ILiveShareService service)
    {
        _service = service;
    }

    [HttpGet("{token}")]
    [EnableRateLimiting(RateLimitingExtensions.Policies.PublicLive)]
    public async Task<IActionResult> Get(string token, [FromQuery] bool trail = true)
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";

        var response = await _service.GetPublicViewAsync(token, trail);
        return StatusCode(response.StatusCode, response);
    }
}