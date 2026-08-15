using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.SavedPlace;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class SavedPlaceService : ISavedPlaceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    // Not client-configurable -- a deliberate, consistent radius for every
    // place, rather than something a request could set arbitrarily small
    // (never triggers) or large (triggers for half the city).
    private const int DefaultRadiusMeters = 150;

    public SavedPlaceService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<SavedPlaceResponseDto>>> GetMyPlacesAsync()
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
        {
            return ApiResponse<IReadOnlyList<SavedPlaceResponseDto>>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var places = await _unitOfWork.SavedPlaces.GetByCustomerAsync(customer.CustomerId);
        var dtoList = places.Select(ToDto).ToList();

        return ApiResponse<IReadOnlyList<SavedPlaceResponseDto>>.Ok(dtoList, "Places retrieved successfully.");
    }

    public async Task<ApiResponse<SavedPlaceResponseDto>> AddPlaceAsync(CreateSavedPlaceDto dto)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
        {
            return ApiResponse<SavedPlaceResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ApiResponse<SavedPlaceResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Name is required.", "A place needs a name.");
        }

        var place = new SavedPlace
        {
            PlaceId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            Name = dto.Name.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            RadiusMeters = DefaultRadiusMeters,
            VisitCount = 0,
            LastVisitedAt = null,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.SavedPlaces.AddAsync(place);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<SavedPlaceResponseDto>.Ok(ToDto(place), "Place saved.");
    }

    public async Task<ApiResponse<string>> DeletePlaceAsync(Guid placeId)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var place = await _unitOfWork.SavedPlaces.GetByIdAsync(placeId);
        if (place is null || (!_currentUser.IsStaff && place.CustomerId != customer.CustomerId))
        {
            // Same 404-not-403 pattern used everywhere else in this API --
            // doesn't reveal that a place ID exists but belongs to someone
            // else.
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Place not found.", $"No saved place exists with ID '{placeId}'.");
        }

        _unitOfWork.SavedPlaces.Remove(place);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<string>.Ok("OK", "Place removed.");
    }

    private async Task<Customer?> ResolveCustomerAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid)) return null;
        return await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
    }

    private static SavedPlaceResponseDto ToDto(SavedPlace place)
    {
        return new SavedPlaceResponseDto
        {
            PlaceId = place.PlaceId,
            Name = place.Name,
            Latitude = place.Latitude,
            Longitude = place.Longitude,
            RadiusMeters = place.RadiusMeters,
            VisitCount = place.VisitCount,
            LastVisitedAt = place.LastVisitedAt,
            CreatedAt = place.CreatedAt
        };
    }
}