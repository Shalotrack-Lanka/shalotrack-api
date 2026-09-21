using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.Complaint;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

// Kept as a separate controller from InternalController (which is
// GET-sync-endpoint focused) for organization, but protected the exact
// same way -- AdminSyncKeyMiddleware matches on the "/api/internal" path
// prefix, not on controller name, so this route group is covered
// automatically with no new middleware wiring needed.
[ApiController]
[Route("api/internal/complaints")]
[AllowAnonymous]
public class InternalComplaintsController : ControllerBase
{
    private readonly IComplaintService _complaintService;

    public InternalComplaintsController(IComplaintService complaintService)
    {
        _complaintService = complaintService;
    }

    [HttpGet("by-dealer/{dealerId:int}")]
    public async Task<IActionResult> GetByDealer(int dealerId)
    {
        var response = await _complaintService.GetByDealerAsync(dealerId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("for-admin")]
    public async Task<IActionResult> GetForAdmin()
    {
        var response = await _complaintService.GetForAdminAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{complaintId:guid}/reply")]
    public async Task<IActionResult> Reply(Guid complaintId, [FromBody] InternalPostComplaintReplyDto dto)
    {
        var response = await _complaintService.AddInternalReplyAsync(complaintId, dto);
        return StatusCode(response.StatusCode, response);
    }

    // dealerId is an optional query param -- Laravel's AdminComplaintController
    // omits it entirely (admin keeps unrestricted access), while
    // DealerComplaintController always passes its own dealer's ID so the
    // service can reject anything that isn't actually that dealer's
    // complaint.
    [HttpPost("{complaintId:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid complaintId, [FromQuery] int? dealerId)
    {
        var response = await _complaintService.EscalateAsync(complaintId, dealerId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{complaintId:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid complaintId, [FromQuery] int? dealerId)
    {
        var response = await _complaintService.ResolveAsync(complaintId, dealerId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{complaintId:guid}/close")]
    public async Task<IActionResult> Close(Guid complaintId, [FromQuery] int? dealerId)
    {
        var response = await _complaintService.CloseAsync(complaintId, dealerId);
        return StatusCode(response.StatusCode, response);
    }
}