using System.Net;
using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Auth;
using ShaloTrack_API.Constants;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.Account;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Request / cancel / status for "delete my account". Always acts on the customer resolved from the
/// token (no id anywhere). The actual erasure happens 30 days later in AccountPurgeService.
///
/// On request (one transaction): the account is flagged, push tokens are deleted (so no notification
/// of any kind can reach the phone), live-share links are revoked and vehicle shares are ended. Those
/// last three are NOT restored by a cancel: re-inviting is the safe failure mode (someone leaving
/// because of a bad relationship must not keep being watched for 30 days).
/// After the commit, Firebase sign-ins are revoked (best effort) and the lock cache is refreshed.
/// </summary>
public class AccountDeletionService : IAccountDeletionService
{
    private readonly ShaloTrackDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFirebaseUserAdmin _firebase;
    private readonly IAccountLockCache _locks;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        ShaloTrackDbContext db,
        ICurrentUser currentUser,
        IFirebaseUserAdmin firebase,
        IAccountLockCache locks,
        ILogger<AccountDeletionService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _firebase = firebase;
        _locks = locks;
        _logger = logger;
    }

    public async Task<ApiResponse<DeletionStatusDto>> GetStatusAsync()
    {
        var me = await FindMeAsync();
        if (me is null) return Unauthorized();

        return ApiResponse<DeletionStatusDto>.Ok(ToStatus(me.DeletionRequestedAt), "Deletion status retrieved.");
    }

    public async Task<ApiResponse<DeletionStatusDto>> RequestAsync(DeleteAccountRequestDto dto)
    {
        var me = await FindMeAsync();
        if (me is null) return Unauthorized();

        if (!string.Equals(dto?.Confirm?.Trim(), AccountDeletion.ConfirmWord, StringComparison.Ordinal))
        {
            return ApiResponse<DeletionStatusDto>.Fail(
                (int)HttpStatusCode.BadRequest,
                $"Type {AccountDeletion.ConfirmWord} to confirm.",
                "Confirmation text does not match.");
        }

        // Already pending: answer with the existing schedule (idempotent, never moves the date).
        if (me.DeletionRequestedAt is not null)
            return ApiResponse<DeletionStatusDto>.Ok(ToStatus(me.DeletionRequestedAt), "Deletion is already scheduled.");

        // A recent sign-in is required for something this destructive.
        var signedInAt = _currentUser.AuthTimeUtc;
        if (signedInAt is null || DateTime.UtcNow - signedInAt.Value > AccountDeletion.MaxSignInAge)
        {
            return ApiResponse<DeletionStatusDto>.Fail(
                (int)HttpStatusCode.Forbidden,
                $"For your security, please sign out and sign in again, then confirm within {(int)AccountDeletion.MaxSignInAge.TotalMinutes} minutes.",
                AccountDeletion.ReauthErrorCode);
        }

        var uid = _currentUser.FirebaseUid!;
        var id = me.CustomerId;
        var now = DateTime.UtcNow;

        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            // Claim: only one request can flip null -> now.
            var claimed = await _db.Customers
                .Where(c => c.CustomerId == id && c.DeletionRequestedAt == null && c.DeletedAt == null)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.DeletionRequestedAt, (DateTime?)now)
                    .SetProperty(c => c.UpdatedAt, now));

            if (claimed == 0)
            {
                await tx.RollbackAsync();
                var current = await FindMeAsync();
                return ApiResponse<DeletionStatusDto>.Ok(ToStatus(current?.DeletionRequestedAt), "Deletion is already scheduled.");
            }

            await _db.CustomerFcmTokens.Where(t => t.CustomerId == id).ExecuteDeleteAsync();

            await _db.LiveShareLinks
                .Where(l => l.CustomerId == id && l.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(l => l.RevokedAt, (DateTime?)now));

            // Shares the customer gave: ended (Revoked). Shares they received: declined.
            await _db.VehicleShares
                .Where(v => v.OwnerCustomerId == id
                            && (v.Status == VehicleShareStatus.Pending || v.Status == VehicleShareStatus.Accepted))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(v => v.Status, VehicleShareStatus.Revoked)
                    .SetProperty(v => v.RespondedAt, (DateTime?)now));

            await _db.VehicleShares
                .Where(v => v.SharedWithCustomerId == id
                            && (v.Status == VehicleShareStatus.Pending || v.Status == VehicleShareStatus.Accepted))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(v => v.Status, VehicleShareStatus.Declined)
                    .SetProperty(v => v.RespondedAt, (DateTime?)now));

            await tx.CommitAsync();
        }

        _locks.Invalidate(uid);

        try
        {
            await _firebase.RevokeSessionsAsync(uid);
        }
        catch (Exception ex)
        {
            // The lock already blocks the API; this only shortens how long old sign-ins last.
            _logger.LogWarning(ex, "AccountDeletion: could not revoke Firebase sessions for customer {CustomerId}.", id);
        }

        _logger.LogInformation("AccountDeletion: customer {CustomerId} requested deletion.", id);

        return ApiResponse<DeletionStatusDto>.Ok(
            ToStatus(now),
            $"Your account is scheduled for permanent deletion in {AccountDeletion.GraceDays} days. Sign in before then to cancel.");
    }

    public async Task<ApiResponse<DeletionStatusDto>> CancelAsync()
    {
        var me = await FindMeAsync();
        if (me is null) return Unauthorized();

        var now = DateTime.UtcNow;
        var changed = await _db.Customers
            .Where(c => c.CustomerId == me.CustomerId && c.DeletionRequestedAt != null && c.DeletedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.DeletionRequestedAt, (DateTime?)null)
                .SetProperty(c => c.UpdatedAt, now));

        _locks.Invalidate(_currentUser.FirebaseUid!);

        if (changed > 0)
            _logger.LogInformation("AccountDeletion: customer {CustomerId} cancelled deletion.", me.CustomerId);

        return ApiResponse<DeletionStatusDto>.Ok(
            ToStatus(null),
            changed > 0 ? "Account deletion cancelled." : "No deletion was scheduled.");
    }

    // -------------------------------------------------------------------------

    private async Task<MeRow?> FindMeAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid)) return null;

        return await _db.Customers.AsNoTracking()
            .Where(c => c.FirebaseUid == uid && c.DeletedAt == null)
            .Select(c => new MeRow(c.CustomerId, c.DeletionRequestedAt))
            .FirstOrDefaultAsync();
    }

    private static DeletionStatusDto ToStatus(DateTime? requestedAt)
    {
        if (requestedAt is null)
            return new DeletionStatusDto { Pending = false, GraceDays = AccountDeletion.GraceDays };

        var scheduled = requestedAt.Value.AddDays(AccountDeletion.GraceDays);
        var daysLeft = (int)Math.Ceiling((scheduled - DateTime.UtcNow).TotalDays);

        return new DeletionStatusDto
        {
            Pending = true,
            RequestedAt = requestedAt,
            ScheduledFor = scheduled,
            DaysLeft = Math.Max(0, daysLeft),
            GraceDays = AccountDeletion.GraceDays,
        };
    }

    private static ApiResponse<DeletionStatusDto> Unauthorized() =>
        ApiResponse<DeletionStatusDto>.Fail(
            (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");

    private sealed record MeRow(Guid CustomerId, DateTime? DeletionRequestedAt);
}