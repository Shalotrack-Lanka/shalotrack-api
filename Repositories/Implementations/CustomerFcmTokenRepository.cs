using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class CustomerFcmTokenRepository : ICustomerFcmTokenRepository
{
    private readonly ShaloTrackDbContext _context;

    public CustomerFcmTokenRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerFcmToken?> GetByTokenAsync(string fcmToken)
    {
        return await _context.CustomerFcmTokens
            .FirstOrDefaultAsync(t => t.FcmToken == fcmToken);
    }

    public async Task<List<CustomerFcmToken>> GetByCustomerAsync(Guid customerId)
    {
        return await _context.CustomerFcmTokens
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .ToListAsync();
    }

    public async Task AddAsync(CustomerFcmToken token)
    {
        await _context.CustomerFcmTokens.AddAsync(token);
    }

    // ExecuteDeleteAsync runs immediately (no SaveChanges needed) and never loads the row.
    public Task<int> DeleteForCustomerAsync(Guid customerId, string fcmToken)
    {
        return _context.CustomerFcmTokens
            .Where(t => t.CustomerId == customerId && t.FcmToken == fcmToken)
            .ExecuteDeleteAsync();
    }

    public Task<int> DeleteByTokenAsync(string fcmToken)
    {
        return _context.CustomerFcmTokens
            .Where(t => t.FcmToken == fcmToken)
            .ExecuteDeleteAsync();
    }

    public Task<int> ReassignAsync(string fcmToken, Guid customerId, string? platform, DateTime updatedAtUtc)
    {
        var query = _context.CustomerFcmTokens.Where(t => t.FcmToken == fcmToken);
        return string.IsNullOrWhiteSpace(platform)
            ? query.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.CustomerId, customerId)
                .SetProperty(t => t.UpdatedAt, updatedAtUtc))
            : query.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.CustomerId, customerId)
                .SetProperty(t => t.Platform, platform)
                .SetProperty(t => t.UpdatedAt, updatedAtUtc));
    }
}
