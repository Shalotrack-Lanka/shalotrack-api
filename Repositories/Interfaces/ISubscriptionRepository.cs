using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface ISubscriptionRepository
{
    Task<List<Subscription>> GetByCustomerAsync(Guid customerId);
    Task<Subscription?> GetByIdAsync(Guid subscriptionId);
    Task AddAsync(Subscription subscription);
}