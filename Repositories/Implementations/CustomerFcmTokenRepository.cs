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
}