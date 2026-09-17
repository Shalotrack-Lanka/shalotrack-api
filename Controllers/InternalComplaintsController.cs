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

    [HttpPost("{complaintId:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid complaintId)
    {
        var response = await _complaintService.EscalateAsync(complaintId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{complaintId:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid complaintId)
    {
        var response = await _complaintService.ResolveAsync(complaintId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{complaintId:guid}/close")]
    public async Task<IActionResult> Close(Guid complaintId)
    {
        var response = await _complaintService.CloseAsync(complaintId);
        return StatusCode(response.StatusCode, response);
    }
}