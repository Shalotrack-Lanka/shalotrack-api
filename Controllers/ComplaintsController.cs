using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.Complaint;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComplaintsController : ControllerBase
{
    private readonly IComplaintService _complaintService;

    public ComplaintsController(IComplaintService complaintService)
    {
        _complaintService = complaintService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateComplaintDto dto)
    {
        var response = await _complaintService.CreateAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var response = await _complaintService.GetMineAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet("{complaintId:guid}")]
    public async Task<IActionResult> GetById(Guid complaintId)
    {
        var response = await _complaintService.GetByIdAsync(complaintId);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost("{complaintId:guid}/reply")]
    public async Task<IActionResult> Reply(Guid complaintId, [FromBody] CreateComplaintReplyDto dto)
    {
        var response = await _complaintService.AddCustomerReplyAsync(complaintId, dto);
        return StatusCode(response.StatusCode, response);
    }
}