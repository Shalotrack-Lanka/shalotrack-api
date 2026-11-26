using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.VehicleShare;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Sharing a vehicle with another person, by their mobile number.
///
/// Privacy rule: an invite never tells the inviter whether the number has a ShaloTrack account. The reply,
/// the status code, the work done before replying and the owner's list are the same either way, and the
/// recipient's name is shown to the owner only after the recipient accepts. Without this, the invite form
/// answers "does this number use ShaloTrack, and what is that person's name?" for any number typed into it.
/// </summary>
public class VehicleShareService : IVehicleShareService
{
    /// <summary>Live pending invites one owner may have at once. Bounds rows created by invites to numbers
    /// nobody uses; a real family shares with a handful of people.</summary>
    public const int MaxPendingInvitesPerOwner = 20;

    public const string InviteSentMessage = "Invite sent. If this number uses ShaloTrack, they'll see it in Sharing.";

    private readonly IUnitOfWork _unitOfWork;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VehicleShareService> _logger;

    public VehicleShareService(IUnitOfWork unitOfWork, IServiceScopeFactory scopeFactory, ILogger<VehicleShareService> logger)
    {
        _unitOfWork = unitOfWork;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private static DateTime PendingSince(DateTime now) => now - VehicleShare.PendingLifetime;

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
        if (vehicle is null || vehicle.CustomerId != owner.CustomerId || !vehicle.IsActive)
        {
            return ApiResponse<VehicleShareResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Vehicle not found.", "No vehicle with this ID belongs to this account.");
        }

        // Format only: says nothing about accounts.
        if (!PhoneNumberNormalizer.TryNormalize(dto.PhoneNumber, out var invitedE164))
        {
            return ApiResponse<VehicleShareResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Enter a Sri Lankan mobile number, for example 077 123 4567.", "");
        }

        // The owner knows their own number, so this reveals nothing.
        if (PhoneNumberNormalizer.TryNormalize(owner.PhoneNumber, out var ownerE164) && ownerE164 == invitedE164)
        {
            return ApiResponse<VehicleShareResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "That's your own number.", "");
        }

        var now = DateTime.UtcNow;
        var pendingSince = PendingSince(now);

        // Always looked up, whether or not it is used below, so both paths do the same work.
        var holder = await _unitOfWork.Customers.GetByPhoneNumberAsync(invitedE164);
        if (holder is not null && holder.CustomerId == owner.CustomerId) holder = null;

        // Only matches rows the owner already sees in their own list (their pending invite to this number, or a
        // person who accepted), so refusing a duplicate tells them nothing new.
        if (await _unitOfWork.VehicleShares.HasActiveShareAsync(vehicle.VehicleId, invitedE164, holder?.CustomerId, pendingSince))
        {
            return ApiResponse<VehicleShareResponseDto>.Fail(
                (int)HttpStatusCode.Conflict, "You've already shared this vehicle with this number.",
                "There is a pending invite or an accepted share for this number on this vehicle.");
        }

        if (await _unitOfWork.VehicleShares.CountPendingInvitesAsync(owner.CustomerId, pendingSince) >= MaxPendingInvitesPerOwner)
        {
            return ApiResponse<VehicleShareResponseDto>.Fail(
                (int)HttpStatusCode.Conflict, $"You have {MaxPendingInvitesPerOwner} invites waiting. Cancel some before sending more.", "");
        }

        var share = new VehicleShare
        {
            ShareId = Guid.NewGuid(),
            VehicleId = vehicle.VehicleId,
            OwnerCustomerId = owner.CustomerId,
            InvitedPhoneNumber = invitedE164,
            SharedWithCustomerId = null,
            Status = VehicleShareStatus.Pending,
            InvitedAt = now
        };

        await _unitOfWork.VehicleShares.AddAsync(share);
        await _unitOfWork.SaveChangesAsync();

        // Sent after the reply is decided and off the request path, so how long the request takes doesn't
        // reveal whether there was anyone to notify.
        if (holder is not null)
        {
            QueueInvitePush(holder.CustomerId, owner.FullName, vehicle.VehicleNumber);
        }

        return ApiResponse<VehicleShareResponseDto>.Ok(ToOwnerDto(share, vehicle.VehicleNumber, vehicle.Make, vehicle.Model, now), InviteSentMessage);
    }

    private void QueueInvitePush(Guid recipientCustomerId, string ownerName, string vehicleNumber)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                await push.SendAlertPushAsync(
                    recipientCustomerId,
                    "Vehicle share invite",
                    $"{ownerName} wants to share {vehicleNumber} with you.");
            }
            catch (Exception ex)
            {
                // Best effort: the invite is saved and shows in the recipient's Sharing screen either way.
                _logger.LogWarning(ex, "Share invite push failed for customer {CustomerId}.", recipientCustomerId);
            }
        });
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

        var now = DateTime.UtcNow;
        var changed = PhoneNumberNormalizer.TryNormalize(responder.PhoneNumber, out var responderE164)
            ? await _unitOfWork.VehicleShares.RespondAsync(shareId, responder.CustomerId, responderE164, dto.Accept, PendingSince(now), now)
            : 0;

        if (changed == 0)
        {
            // Same reply for "not yours", "already answered", "expired" and "cancelled by the owner".
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "This invite is no longer available.",
                "It may have been answered, cancelled by the owner, or expired.");
        }

        var share = await _unitOfWork.VehicleShares.GetByIdAsync(shareId);
        if (share is not null)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var push = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                await push.SendAlertPushAsync(
                    share.OwnerCustomerId,
                    "Vehicle share update",
                    $"{responder.FullName} {(dto.Accept ? "accepted" : "declined")} your share invite for {share.Vehicle.VehicleNumber}.");
            }
            catch (Exception ex)
            {
                // The answer itself is saved; the push is best effort.
                _logger.LogWarning(ex, "Share response push failed for owner {CustomerId}.", share.OwnerCustomerId);
            }
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

        var changed = await _unitOfWork.VehicleShares.RevokeAsync(shareId, owner.CustomerId, DateTime.UtcNow);
        if (changed == 0)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Share not found.", "No active share or pending invite with this ID belongs to this account.");
        }

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

        var now = DateTime.UtcNow;
        var shares = await _unitOfWork.VehicleShares.GetOwnedSharesAsync(owner.CustomerId, vehicleId);
        var dtos = shares.Select(s => ToOwnerDto(s, s.Vehicle.VehicleNumber, s.Vehicle.Make, s.Vehicle.Model, now)).ToList();
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
        var dtos = shares.Select(ToRecipientDto).ToList();
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

        if (!PhoneNumberNormalizer.TryNormalize(viewer.PhoneNumber, out var viewerE164))
        {
            return ApiResponse<List<VehicleShareResponseDto>>.Ok(new List<VehicleShareResponseDto>(), "Pending invites retrieved.");
        }

        var shares = await _unitOfWork.VehicleShares.GetPendingInvitesForAsync(viewer.CustomerId, viewerE164, PendingSince(DateTime.UtcNow));
        var dtos = shares.Select(ToRecipientDto).ToList();
        return ApiResponse<List<VehicleShareResponseDto>>.Ok(dtos, "Pending invites retrieved.");
    }

    /// <summary>
    /// The owner's view. The other person's name and current number appear only once they have accepted;
    /// before that the owner sees just the number they typed, so the list can't be used to look people up.
    /// A pending invite past its lifetime is reported as "Expired".
    /// </summary>
    private static VehicleShareResponseDto ToOwnerDto(VehicleShare share, string vehicleNumber, string make, string model, DateTime now)
    {
        var accepted = share.Status == VehicleShareStatus.Accepted && share.SharedWithCustomer is not null;
        var expired = share.Status == VehicleShareStatus.Pending && share.InvitedAt < PendingSince(now);
        return new VehicleShareResponseDto
        {
            ShareId = share.ShareId,
            Status = expired ? "Expired" : share.Status.ToString(),
            InvitedAt = share.InvitedAt,
            RespondedAt = share.RespondedAt,
            VehicleId = share.VehicleId,
            VehicleNumber = vehicleNumber,
            Make = make,
            Model = model,
            OtherPartyCustomerId = accepted ? share.SharedWithCustomer!.CustomerId : Guid.Empty,
            OtherPartyName = accepted ? share.SharedWithCustomer!.FullName : string.Empty,
            OtherPartyPhoneNumber = accepted ? share.SharedWithCustomer!.PhoneNumber : share.InvitedPhoneNumber
        };
    }

    /// <summary>The recipient's view: who is sharing with them (the owner chose to contact them).</summary>
    private static VehicleShareResponseDto ToRecipientDto(VehicleShare share) => new()
    {
        ShareId = share.ShareId,
        Status = share.Status.ToString(),
        InvitedAt = share.InvitedAt,
        RespondedAt = share.RespondedAt,
        VehicleId = share.VehicleId,
        VehicleNumber = share.Vehicle.VehicleNumber,
        Make = share.Vehicle.Make,
        Model = share.Vehicle.Model,
        OtherPartyCustomerId = share.OwnerCustomer.CustomerId,
        OtherPartyName = share.OwnerCustomer.FullName,
        OtherPartyPhoneNumber = share.OwnerCustomer.PhoneNumber
    };
}