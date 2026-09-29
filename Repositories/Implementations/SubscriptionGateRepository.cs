using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class SubscriptionGateRepository : ISubscriptionGateRepository
{
    private readonly ShaloTrackDbContext _context;

    public SubscriptionGateRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<DeviceSubscriptionStatus?> GetByImeiAsync(string imei)
    {
        string trimmedImei = imei.Trim();
        return await _context.DeviceSubscriptionStatuses
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ImeiNumber.Trim() == trimmedImei);
    }

    public async Task UpsertAsync(string imei, bool isActive, DateTime? expiresAt)
    {
        var existing = await _context.DeviceSubscriptionStatuses
            .FirstOrDefaultAsync(d => d.ImeiNumber == imei);

        if (existing is null)
        {
            await _context.DeviceSubscriptionStatuses.AddAsync(new DeviceSubscriptionStatus
            {
                ImeiNumber = imei,
                IsActive = isActive,
                ExpiresAt = expiresAt,
                UpdatedAt = DateTime.UtcNow,
            });
        }
        else
        {
            existing.IsActive = isActive;
            existing.ExpiresAt = expiresAt;
            existing.UpdatedAt = DateTime.UtcNow;
        }
    }
}