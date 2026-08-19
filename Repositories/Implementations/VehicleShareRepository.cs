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
        return await _context.VehicleShares
            .FirstOrDefaultAsync(s => s.VehicleId == vehicleId
                && s.SharedWithCustomerId == sharedWithCustomerId
                && s.Status != VehicleShareStatus.Revoked
                && s.Status != VehicleShareStatus.Declined);
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
            .Include(s => s.OwnerCustomer)
            .Where(s => s.SharedWithCustomerId == sharedWithCustomerId && s.Status == VehicleShareStatus.Accepted)
            .OrderByDescending(s => s.RespondedAt)
            .ToListAsync();
    }

    public async Task<List<VehicleShare>> GetPendingInvitesForAsync(Guid sharedWithCustomerId)
    {
        return await _context.VehicleShares
            .AsNoTracking()
            .Include(s => s.Vehicle)
            .Include(s => s.OwnerCustomer)
            .Where(s => s.SharedWithCustomerId == sharedWithCustomerId && s.Status == VehicleShareStatus.Pending)
            .OrderByDescending(s => s.InvitedAt)
            .ToListAsync();
    }

    public async Task<List<VehicleShare>> GetAcceptedSharesForVehicleAsync(Guid vehicleId)
    {
        return await _context.VehicleShares
            .AsNoTracking()
            .Where(s => s.VehicleId == vehicleId && s.Status == VehicleShareStatus.Accepted)
            .ToListAsync();
    }
}