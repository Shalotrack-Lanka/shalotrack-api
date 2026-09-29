using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface ISubscriptionGateRepository
{
    Task<DeviceSubscriptionStatus?> GetByImeiAsync(string imei);
    Task UpsertAsync(string imei, bool isActive, DateTime? expiresAt);
}