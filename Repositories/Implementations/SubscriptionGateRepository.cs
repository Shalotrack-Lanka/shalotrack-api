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
        // The DbContext is NoTracking globally, so ask for tracking explicitly, or the
        // changes below are never saved.
        var existing = await _context.DeviceSubscriptionStatuses
            .AsTracking()
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

    public async Task<int> UpsertManyAsync(IEnumerable<(string Imei, bool IsActive, DateTime? ExpiresAt)> items)
    {
        // Last entry wins if an IMEI is repeated in one request.
        var wanted = items
            .GroupBy(i => i.Imei)
            .Select(g => g.Last())
            .ToList();
        if (wanted.Count == 0) return 0;

        var imeis = wanted.Select(w => w.Imei).ToList();
        // The DbContext is NoTracking globally, so ask for tracking explicitly, or updates to
        // existing rows are silently not saved (only inserts would work).
        var existing = await _context.DeviceSubscriptionStatuses
            .AsTracking()
            .Where(d => imeis.Contains(d.ImeiNumber))
            .ToDictionaryAsync(d => d.ImeiNumber);

        var now = DateTime.UtcNow;
        foreach (var (imei, isActive, expiresAt) in wanted)
        {
            if (existing.TryGetValue(imei, out var row))
            {
                row.IsActive = isActive;
                row.ExpiresAt = expiresAt;
                row.UpdatedAt = now;
            }
            else
            {
                _context.DeviceSubscriptionStatuses.Add(new DeviceSubscriptionStatus
                {
                    ImeiNumber = imei,
                    IsActive = isActive,
                    ExpiresAt = expiresAt,
                    UpdatedAt = now,
                });
            }
        }

        await _context.SaveChangesAsync();
        return wanted.Count;
    }
}