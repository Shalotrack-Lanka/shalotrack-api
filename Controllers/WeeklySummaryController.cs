using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.WeeklySummary;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WeeklySummaryController : ControllerBase
{
    private readonly IWeeklySummarySettingsService _service;

    public WeeklySummaryController(IWeeklySummarySettingsService service)
    {
        _service = service;
    }

    /// <summary>The signed-in customer's weekly summary switch.</summary>
    [HttpGet("settings")]
    public async Task<IActionResult> Get()
    {
        var response = await _service.GetAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> Update([FromBody] WeeklySummarySettingsDto dto)
    {
        var response = await _service.UpdateAsync(dto);
        return StatusCode(response.StatusCode, response);
    }
}