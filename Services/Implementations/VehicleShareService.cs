using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.VehicleShare;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class VehicleShareService : IVehicleShareService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPushNotificationService _pushNotificationService;

    public VehicleShareService(IUnitOfWork unitOfWork, IPushNotificationService pushNotificationService)
    {
        _unitOfWork = unitOfWork;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<ApiResponse<VehicleShareResponseDto>> InviteAsync(string ownerFirebaseUid, InviteVehicleShareDto dto)
    {
        if (string.IsNullOrEmpty(ownerFirebaseUid))
        {
            return ApiResponse<VehicleShareResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var owner = await _unitOfWork.Customers.GetByFirebaseUidAsync(ownerFirebaseUid);
        if (owner is null)
        {
            return ApiResponse<VehicleShareResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(dto.VehicleId);
        if (vehicle is null || vehicle.CustomerId != owner.CustomerId)
        {
            return ApiResponse<VehicleShareResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Vehicle not found.", $"No vehicle exists with ID '{dto.VehicleId}'.");
        }

        var sharedWith = await _unitOfWork.Customers.GetByPhoneNumberAsync(dto.PhoneNumber);
        if (sharedWith is null)
        {
            return ApiResponse<VehicleShareResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "No account found for that number.",
                "The person you're inviting needs to have the ShaloTrack app installed and registered with this phone number first.");
        }

        if (sharedWith.CustomerId == owner.CustomerId)
        {
            return ApiResponse<VehicleShareResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "You can't share a vehicle with yourself.", "");
        }

        var existing = await _unitOfWork.VehicleShares.GetByVehicleAndSharedWithAsync(dto.VehicleId, sharedWith.CustomerId);
        if (existing is not null)
        {
            return ApiResponse<VehicleShareResponseDto>.Fail(
                (int)HttpStatusCode.Conflict, "Already shared with this person.",
                existing.Status == VehicleShareStatus.Pending
                    ? "There's already a pending invite for this person on this vehicle."
                    : "This vehicle is already shared with this person.");
        }

        var share = new VehicleShare
        {
            ShareId = Guid.NewGuid(),
            VehicleId = dto.VehicleId,
            OwnerCustomerId = owner.CustomerId,
            SharedWithCustomerId = sharedWith.CustomerId,
            Status = VehicleShareStatus.Pending,
            InvitedAt = DateTime.UtcNow
        };

        await _unitOfWork.VehicleShares.AddAsync(share);
        await _unitOfWork.SaveChangesAsync();

        // Best-effort -- a failed push shouldn't undo an already-created
        // invite. Same lesson learned from the real SOS 500 investigation:
        // never let a notification failure mask a successful core action.
        try
        {
            await _pushNotificationService.SendAlertPushAsync(
                sharedWith.CustomerId,
                "Vehicle share invite",
                $"{owner.FullName} wants to share {vehicle.VehicleNumber} with you.");
        }
        catch (Exception)
        {
            // Logged inside SendAlertPushAsync itself already -- the
            // invite still succeeded and is visible next time they open
            // the app regardless of whether the push landed.
        }

        var responseDto = new VehicleShareResponseDto
        {
            ShareId = share.ShareId,
            Status = share.Status.ToString(),
            InvitedAt = share.InvitedAt,
            RespondedAt = share.RespondedAt,
            VehicleId = vehicle.VehicleId,
            VehicleNumber = vehicle.VehicleNumber,
            Make = vehicle.Make,
            Model = vehicle.Model,
            OtherPartyCustomerId = sharedWith.CustomerId,
            OtherPartyName = sharedWith.FullName,
            OtherPartyPhoneNumber = sharedWith.PhoneNumber
        };

        return ApiResponse<VehicleShareResponseDto>.Ok(responseDto, "Invite sent.");
    }

    public async Task<ApiResponse<string>> RespondAsync(string responderFirebaseUid, Guid shareId, RespondToVehicleShareDto dto)
    {
        if (string.IsNullOrEmpty(responderFirebaseUid))
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var responder = await _unitOfWork.Customers.GetByFirebaseUidAsync(responderFirebaseUid);
        if (responder is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var share = await _unitOfWork.VehicleShares.GetByIdAsync(shareId);
        if (share is null || share.SharedWithCustomerId != responder.CustomerId)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Invite not found.", $"No pending invite exists with ID '{shareId}' for this account.");
        }

        if (share.Status != VehicleShareStatus.Pending)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.Conflict, "This invite has already been responded to.", $"Current status: {share.Status}.");
        }

        share.Status = dto.Accept ? VehicleShareStatus.Accepted : VehicleShareStatus.Declined;
        share.RespondedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        try
        {
            await _pushNotificationService.SendAlertPushAsync(
                share.OwnerCustomerId,
                "Vehicle share update",
                $"{responder.FullName} {(dto.Accept ? "accepted" : "declined")} your share invite for {share.Vehicle.VehicleNumber}.");
        }
        catch (Exception)
        {
            // Same reasoning as InviteAsync -- the response itself already succeeded.
        }

        return ApiResponse<string>.Ok("OK", dto.Accept ? "Invite accepted." : "Invite declined.");
    }

    public async Task<ApiResponse<string>> RevokeAsync(string ownerFirebaseUid, Guid shareId)
    {
        if (string.IsNullOrEmpty(ownerFirebaseUid))
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var owner = await _unitOfWork.Customers.GetByFirebaseUidAsync(ownerFirebaseUid);
        if (owner is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var share = await _unitOfWork.VehicleShares.GetByIdAsync(shareId);
        if (share is null || share.OwnerCustomerId != owner.CustomerId)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Share not found.", $"No share exists with ID '{shareId}' owned by this account.");
        }

        share.Status = VehicleShareStatus.Revoked;
        share.RespondedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<string>.Ok("OK", "Share revoked.");
    }

    public async Task<ApiResponse<List<VehicleShareResponseDto>>> GetMySharesAsync(string ownerFirebaseUid, Guid? vehicleId)
    {
        if (string.IsNullOrEmpty(ownerFirebaseUid))
        {
            return ApiResponse<List<VehicleShareResponseDto>>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var owner = await _unitOfWork.Customers.GetByFirebaseUidAsync(ownerFirebaseUid);
        if (owner is null)
        {
            return ApiResponse<List<VehicleShareResponseDto>>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var shares = await _unitOfWork.VehicleShares.GetOwnedSharesAsync(owner.CustomerId, vehicleId);
        var dtos = shares.Select(s => MapToDto(s, viewedByOwner: true)).ToList();
        return ApiResponse<List<VehicleShareResponseDto>>.Ok(dtos, "Shares retrieved.");
    }

    public async Task<ApiResponse<List<VehicleShareResponseDto>>> GetSharedWithMeAsync(string viewerFirebaseUid)
    {
        if (string.IsNullOrEmpty(viewerFirebaseUid))
        {
            return ApiResponse<List<VehicleShareResponseDto>>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var viewer = await _unitOfWork.Customers.GetByFirebaseUidAsync(viewerFirebaseUid);
        if (viewer is null)
        {
            return ApiResponse<List<VehicleShareResponseDto>>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var shares = await _unitOfWork.VehicleShares.GetSharedWithMeAsync(viewer.CustomerId);
        var dtos = shares.Select(s => MapToDto(s, viewedByOwner: false)).ToList();
        return ApiResponse<List<VehicleShareResponseDto>>.Ok(dtos, "Shared vehicles retrieved.");
    }

    public async Task<ApiResponse<List<VehicleShareResponseDto>>> GetPendingInvitesAsync(string viewerFirebaseUid)
    {
        if (string.IsNullOrEmpty(viewerFirebaseUid))
        {
            return ApiResponse<List<VehicleShareResponseDto>>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var viewer = await _unitOfWork.Customers.GetByFirebaseUidAsync(viewerFirebaseUid);
        if (viewer is null)
        {
            return ApiResponse<List<VehicleShareResponseDto>>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var shares = await _unitOfWork.VehicleShares.GetPendingInvitesForAsync(viewer.CustomerId);
        var dtos = shares.Select(s => MapToDto(s, viewedByOwner: false)).ToList();
        return ApiResponse<List<VehicleShareResponseDto>>.Ok(dtos, "Pending invites retrieved.");
    }

    // viewedByOwner determines which side's details populate "OtherParty" --
    // the owner sees the shared-with person's info, the shared-with person
    // sees the owner's info.
    private static VehicleShareResponseDto MapToDto(VehicleShare share, bool viewedByOwner)
    {
        var otherParty = viewedByOwner ? share.SharedWithCustomer : share.OwnerCustomer;
        return new VehicleShareResponseDto
        {
            ShareId = share.ShareId,
            Status = share.Status.ToString(),
            InvitedAt = share.InvitedAt,
            RespondedAt = share.RespondedAt,
            VehicleId = share.VehicleId,
            VehicleNumber = share.Vehicle.VehicleNumber,
            Make = share.Vehicle.Make,
            Model = share.Vehicle.Model,
            OtherPartyCustomerId = otherParty.CustomerId,
            OtherPartyName = otherParty.FullName,
            OtherPartyPhoneNumber = otherParty.PhoneNumber
        };
    }
}