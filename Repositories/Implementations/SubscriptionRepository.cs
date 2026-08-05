using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly ShaloTrackDbContext _context;

    public SubscriptionRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<Subscription>> GetByCustomerAsync(Guid customerId)
    {
        return await _context.Subscriptions
            .AsNoTracking()
            .Where(s => s.CustomerId == customerId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<Subscription?> GetByIdAsync(Guid subscriptionId)
    {
        return await _context.Subscriptions
            .Include(s => s.Customer)
            .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
    }

    public async Task AddAsync(Subscription subscription)
    {
        await _context.Subscriptions.AddAsync(subscription);
    }
}