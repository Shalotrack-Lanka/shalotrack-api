using ShaloTrack_API.DTOs.VehicleShare;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IVehicleShareService
{
    Task<ApiResponse<VehicleShareResponseDto>> InviteAsync(string ownerFirebaseUid, InviteVehicleShareDto dto);
    Task<ApiResponse<string>> RespondAsync(string responderFirebaseUid, Guid shareId, RespondToVehicleShareDto dto);
    Task<ApiResponse<string>> RevokeAsync(string ownerFirebaseUid, Guid shareId);
    Task<ApiResponse<List<VehicleShareResponseDto>>> GetMySharesAsync(string ownerFirebaseUid, Guid? vehicleId);
    Task<ApiResponse<List<VehicleShareResponseDto>>> GetSharedWithMeAsync(string viewerFirebaseUid);
    Task<ApiResponse<List<VehicleShareResponseDto>>> GetPendingInvitesAsync(string viewerFirebaseUid);
}