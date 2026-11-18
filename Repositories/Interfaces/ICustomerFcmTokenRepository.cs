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

    /// <summary>
    /// Moves an existing token to this customer (a second person signed in on the same phone). One UPDATE,
    /// runs immediately. (The context is NoTracking by default, so editing a loaded entity and calling SaveChanges writes nothing.) Returns rows changed.
    /// </summary>
    Task<int> ReassignAsync(string fcmToken, Guid customerId, string? platform, DateTime updatedAtUtc);
}
