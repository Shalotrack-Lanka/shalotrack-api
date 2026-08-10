using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SOSController : ControllerBase
{
    private readonly ISOSService _sosService;

    public SOSController(ISOSService sosService)
    {
        _sosService = sosService;
    }

    [HttpPost("{vehicleId:guid}/trigger")]
    public async Task<IActionResult> Trigger(Guid vehicleId)
    {
        var response = await _sosService.TriggerSOSAsync(vehicleId);
        return StatusCode(response.StatusCode, response);
    }
}