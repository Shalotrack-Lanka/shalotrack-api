using ShaloTrack_API.DTOs.SavedPlace;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface ISavedPlaceService
{
    Task<ApiResponse<IReadOnlyList<SavedPlaceResponseDto>>> GetMyPlacesAsync();
    Task<ApiResponse<SavedPlaceResponseDto>> AddPlaceAsync(CreateSavedPlaceDto dto);
    Task<ApiResponse<string>> DeletePlaceAsync(Guid placeId);
}