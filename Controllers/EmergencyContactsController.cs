using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.EmergencyContact;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmergencyContactsController : ControllerBase
{
    private readonly IEmergencyContactService _emergencyContactService;

    public EmergencyContactsController(IEmergencyContactService emergencyContactService)
    {
        _emergencyContactService = emergencyContactService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyContacts()
    {
        var response = await _emergencyContactService.GetMyContactsAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost]
    public async Task<IActionResult> AddContact([FromBody] CreateEmergencyContactDto dto)
    {
        var response = await _emergencyContactService.AddContactAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{emergencyContactId:guid}")]
    public async Task<IActionResult> DeleteContact(Guid emergencyContactId)
    {
        var response = await _emergencyContactService.DeleteContactAsync(emergencyContactId);
        return StatusCode(response.StatusCode, response);
    }
}