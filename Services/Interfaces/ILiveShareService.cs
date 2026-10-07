using ShaloTrack_API.DTOs.LiveShare;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface ILiveShareService
{
    // Owner (authenticated)
    Task<ApiResponse<LiveShareLinkResponseDto>> CreateAsync(Guid vehicleId, CreateLiveShareDto dto);
    Task<ApiResponse<IReadOnlyList<LiveShareLinkResponseDto>>> ListActiveAsync(Guid vehicleId);
    Task<ApiResponse<string>> RevokeAsync(Guid linkId);

    // Anyone holding the link (anonymous)
    Task<ApiResponse<PublicLiveViewDto>> GetPublicViewAsync(string token, bool includeTrail);
}