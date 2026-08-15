using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShaloTrack_API.DTOs.SavedPlace;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SavedPlacesController : ControllerBase
{
    private readonly ISavedPlaceService _savedPlaceService;

    public SavedPlacesController(ISavedPlaceService savedPlaceService)
    {
        _savedPlaceService = savedPlaceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyPlaces()
    {
        var response = await _savedPlaceService.GetMyPlacesAsync();
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost]
    public async Task<IActionResult> AddPlace([FromBody] CreateSavedPlaceDto dto)
    {
        var response = await _savedPlaceService.AddPlaceAsync(dto);
        return StatusCode(response.StatusCode, response);
    }

    [HttpDelete("{placeId:guid}")]
    public async Task<IActionResult> DeletePlace(Guid placeId)
    {
        var response = await _savedPlaceService.DeletePlaceAsync(placeId);
        return StatusCode(response.StatusCode, response);
    }
}