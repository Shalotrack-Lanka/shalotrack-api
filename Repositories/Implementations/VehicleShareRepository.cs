using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class VehicleShareRepository : IVehicleShareRepository
{
    private readonly ShaloTrackDbContext _context;

    public VehicleShareRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(VehicleShare share)
    {
        await _context.VehicleShares.AddAsync(share);
    }

    public async Task<VehicleShare?> GetByIdAsync(Guid shareId)
    {
        return await _context.VehicleShares
            .Include(s => s.Vehicle)
            .Include(s => s.OwnerCustomer)
            .Include(s => s.SharedWithCustomer)
            .FirstOrDefaultAsync(s => s.ShareId == shareId);
    }

    public async Task<VehicleShare?> GetByVehicleAndSharedWithAsync(Guid vehicleId, Guid sharedWithCustomerId)
    {
        // Only an ACCEPTED share links a person to a vehicle. Pending invites are addressed to a phone number
        // and have no SharedWithCustomerId until they are accepted.
        return await _context.VehicleShares
            .FirstOrDefaultAsync(s => s.VehicleId == vehicleId
                && s.SharedWithCustomerId == sharedWithCustomerId
                && s.Status == VehicleShareStatus.Accepted);
    }

    public async Task<bool> HasActiveShareAsync(Guid vehicleId, string invitedE164, Guid? currentHolderCustomerId, DateTime pendingSince)
    {
        return await _context.VehicleShares.AnyAsync(s => s.VehicleId == vehicleId
            && ((s.Status == VehicleShareStatus.Pending && s.InvitedAt >= pendingSince && s.InvitedPhoneNumber == invitedE164)
                || (s.Status == VehicleShareStatus.Accepted
                    && (s.InvitedPhoneNumber == invitedE164
                        || (currentHolderCustomerId != null && s.SharedWithCustomerId == currentHolderCustomerId)))));
    }

    public async Task<int> CountPendingInvitesAsync(Guid ownerCustomerId, DateTime pendingSince)
    {
        return await _context.VehicleShares.CountAsync(s => s.OwnerCustomerId == ownerCustomerId
            && s.Status == VehicleShareStatus.Pending
            && s.InvitedAt >= pendingSince);
    }

    public async Task<int> RespondAsync(Guid shareId, Guid responderCustomerId, string responderE164, bool accept, DateTime pendingSince, DateTime now)
    {
        // One UPDATE: only a live pending invite addressed to the responder's own number, never their own vehicle.
        // A double tap or a race with Revoke changes nothing the second time.
        var query = _context.VehicleShares.Where(s => s.ShareId == shareId
            && s.Status == VehicleShareStatus.Pending
            && s.InvitedAt >= pendingSince
            && s.InvitedPhoneNumber == responderE164
            && s.OwnerCustomerId != responderCustomerId
            && s.Vehicle.IsActive);

        return accept
            ? await query.ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, VehicleShareStatus.Accepted)
                .SetProperty(s => s.SharedWithCustomerId, (Guid?)responderCustomerId)
                .SetProperty(s => s.RespondedAt, (DateTime?)now))
            : await query.ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, VehicleShareStatus.Declined)
                .SetProperty(s => s.RespondedAt, (DateTime?)now));
    }

    public async Task<int> RevokeAsync(Guid shareId, Guid ownerCustomerId, DateTime now)
    {
        return await _context.VehicleShares
            .Where(s => s.ShareId == shareId
                && s.OwnerCustomerId == ownerCustomerId
                && (s.Status == VehicleShareStatus.Pending || s.Status == VehicleShareStatus.Accepted))
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, VehicleShareStatus.Revoked)
                .SetProperty(s => s.RespondedAt, (DateTime?)now));
    }

    public async Task<List<VehicleShare>> GetOwnedSharesAsync(Guid ownerCustomerId, Guid? vehicleId = null)
    {
        var query = _context.VehicleShares
            .AsNoTracking()
            .Include(s => s.Vehicle)
            .Include(s => s.SharedWithCustomer)
            .Where(s => s.OwnerCustomerId == ownerCustomerId);

        if (vehicleId.HasValue)
        {
            query = query.Where(s => s.VehicleId == vehicleId.Value);
        }

        return await query.OrderByDescending(s => s.InvitedAt).ToListAsync();
    }

    public async Task<List<VehicleShare>> GetSharedWithMeAsync(Guid sharedWithCustomerId)
    {
        return await _context.VehicleShares
            .AsNoTracking()
            .Include(s => s.Vehicle)
                .ThenInclude(v => v.CurrentLocation) // needed to merge into the dashboard with live location data
            .Include(s => s.OwnerCustomer)
            // FIX: a share of a vehicle the owner has since deleted (IsActive = false)
            // must disappear for the person it was shared with, too.
            .Where(s => s.SharedWithCustomerId == sharedWithCustomerId
                        && s.Status == VehicleShareStatus.Accepted
                        && s.Vehicle.IsActive)
            .OrderByDescending(s => s.RespondedAt)
            .ToListAsync();
    }

    public async Task<List<VehicleShare>> GetPendingInvitesForAsync(Guid viewerCustomerId, string viewerE164, DateTime pendingSince)
    {
        return await _context.VehicleShares
            .AsNoTracking()
            .Include(s => s.Vehicle)
            .Include(s => s.OwnerCustomer)
            .Where(s => s.InvitedPhoneNumber == viewerE164
                        && s.Status == VehicleShareStatus.Pending
                        && s.InvitedAt >= pendingSince
                        && s.OwnerCustomerId != viewerCustomerId
                        && s.Vehicle.IsActive)
            .OrderByDescending(s => s.InvitedAt)
            .ToListAsync();
    }

    public async Task<List<VehicleShare>> GetAcceptedSharesForVehicleAsync(Guid vehicleId)
    {
        return await _context.VehicleShares
            .AsNoTracking()
            .Where(s => s.VehicleId == vehicleId
                        && s.Status == VehicleShareStatus.Accepted
                        && s.SharedWithCustomerId != null)
            .ToListAsync();
    }
}