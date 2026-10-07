using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.LiveShare;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

/// <summary>Owner-only management of temporary live links. Always authenticated.</summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LiveShareController : ControllerBase
{
    private readonly ILiveShareService _service;

    public LiveShareController(ILiveShareService service)
    {
        _service = service;
    }

    [HttpPost("vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> Create(Guid vehicleId, [FromBody] CreateLiveShareDto dto)
    {
        var response = await _service.CreateAsync(vehicleId, dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("vehicle/{vehicleId:guid}")]
    public async Task<IActionResult> List(Guid vehicleId)
    {
        var response = await _service.ListActiveAsync(vehicleId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{linkId:guid}")]
    public async Task<IActionResult> Revoke(Guid linkId)
    {
        var response = await _service.RevokeAsync(linkId);
        return StatusCode(response.StatusCode, response);
    }
}