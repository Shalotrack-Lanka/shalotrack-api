using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface ISubscriptionGateRepository
{
    Task<DeviceSubscriptionStatus?> GetByImeiAsync(string imei);
    Task UpsertAsync(string imei, bool isActive, DateTime? expiresAt);

    /// <summary>Upserts many IMEIs with one read and one SaveChanges (atomic). Returns the number of rows written.</summary>
    Task<int> UpsertManyAsync(IEnumerable<(string Imei, bool IsActive, DateTime? ExpiresAt)> items);
}