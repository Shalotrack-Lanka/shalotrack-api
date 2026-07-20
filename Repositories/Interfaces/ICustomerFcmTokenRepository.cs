using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface ICustomerFcmTokenRepository
{
    Task<CustomerFcmToken?> GetByTokenAsync(string fcmToken);
    Task<List<CustomerFcmToken>> GetByCustomerAsync(Guid customerId);
    Task AddAsync(CustomerFcmToken token);
}