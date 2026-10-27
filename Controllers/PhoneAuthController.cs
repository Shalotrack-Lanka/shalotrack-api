using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShaloTrack_API.DTOs.Auth;
using ShaloTrack_API.Extensions;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

/// <summary>
/// Unauthenticated by design -- this IS the login/signup entry point, so there is no token
/// yet to check. Both actions are the highest-value abuse target in the whole API (free SMS,
/// account-takeover guesses), hence AllowAnonymous + the tightest rate-limit policy we have,
/// on top of the per-phone-number throttling inside PhoneVerificationService itself.
/// </summary>
[ApiController]
[Route("api/auth/otp")]
[AllowAnonymous]
public class PhoneAuthController : ControllerBase
{
    private readonly IPhoneVerificationService _phoneVerificationService;

    public PhoneAuthController(IPhoneVerificationService phoneVerificationService)
    {
        _phoneVerificationService = phoneVerificationService;
    }

    [HttpPost("request")]
    [EnableRateLimiting(RateLimitingExtensions.Policies.OtpRequest)]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var response = await _phoneVerificationService.RequestOtpAsync(dto.PhoneNumber, ip);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("verify")]
    [EnableRateLimiting(RateLimitingExtensions.Policies.OtpVerify)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _phoneVerificationService.VerifyOtpAsync(dto.PhoneNumber, dto.Code);
        return StatusCode(response.StatusCode, response);
    }
}
