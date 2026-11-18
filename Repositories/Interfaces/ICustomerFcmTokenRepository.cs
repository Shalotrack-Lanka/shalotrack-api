using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface ICustomerFcmTokenRepository
{
    Task<CustomerFcmToken?> GetByTokenAsync(string fcmToken);
    Task<List<CustomerFcmToken>> GetByCustomerAsync(Guid customerId);
    Task AddAsync(CustomerFcmToken token);

    /// <summary>Deletes this token row only if it belongs to the given customer. Returns rows deleted (0 or 1).</summary>
    Task<int> DeleteForCustomerAsync(Guid customerId, string fcmToken);

    /// <summary>Deletes a token Firebase reported as dead (app uninstalled, token rotated). Returns rows deleted.</summary>
    Task<int> DeleteByTokenAsync(string fcmToken);
}