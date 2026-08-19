using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.VehicleShare;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VehicleSharesController : ControllerBase
{
    private readonly IVehicleShareService _vehicleShareService;
    private readonly ICurrentUser _currentUser;

    public VehicleSharesController(IVehicleShareService vehicleShareService, ICurrentUser currentUser)
    {
        _vehicleShareService = vehicleShareService;
        _currentUser = currentUser;
    }

    // POST /api/VehicleShares/invite
    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteVehicleShareDto dto)
    {
        var response = await _vehicleShareService.InviteAsync(_currentUser.FirebaseUid!, dto);
        return StatusCode(response.StatusCode, response);
    }

    // POST /api/VehicleShares/{shareId}/respond
    [HttpPost("{shareId:guid}/respond")]
    public async Task<IActionResult> Respond(Guid shareId, [FromBody] RespondToVehicleShareDto dto)
    {
        var response = await _vehicleShareService.RespondAsync(_currentUser.FirebaseUid!, shareId, dto);
        return StatusCode(response.StatusCode, response);
    }

    // DELETE /api/VehicleShares/{shareId}
    [HttpDelete("{shareId:guid}")]
    public async Task<IActionResult> Revoke(Guid shareId)
    {
        var response = await _vehicleShareService.RevokeAsync(_currentUser.FirebaseUid!, shareId);
        return StatusCode(response.StatusCode, response);
    }

    // GET /api/VehicleShares/my-shares?vehicleId={optional}
    [HttpGet("my-shares")]
    public async Task<IActionResult> GetMyShares([FromQuery] Guid? vehicleId)
    {
        var response = await _vehicleShareService.GetMySharesAsync(_currentUser.FirebaseUid!, vehicleId);
        return StatusCode(response.StatusCode, response);
    }

    // GET /api/VehicleShares/shared-with-me
    [HttpGet("shared-with-me")]
    public async Task<IActionResult> GetSharedWithMe()
    {
        var response = await _vehicleShareService.GetSharedWithMeAsync(_currentUser.FirebaseUid!);
        return StatusCode(response.StatusCode, response);
    }

    // GET /api/VehicleShares/pending-invites
    [HttpGet("pending-invites")]
    public async Task<IActionResult> GetPendingInvites()
    {
        var response = await _vehicleShareService.GetPendingInvitesAsync(_currentUser.FirebaseUid!);
        return StatusCode(response.StatusCode, response);
    }
}