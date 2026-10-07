using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ShaloTrack_API.Data;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// 60-second in-memory cache of the pending-deletion flag per Firebase uid, so the lock costs one
/// small query per user per minute rather than one per request. The API runs as one instance; if it
/// is ever scaled out, another instance learns of a change within the TTL.
/// </summary>
public class AccountLockCache : IAccountLockCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IMemoryCache _cache;
    private readonly ShaloTrackDbContext _db;

    public AccountLockCache(IMemoryCache cache, ShaloTrackDbContext db)
    {
        _cache = cache;
        _db = db;
    }

    public async Task<bool> IsPendingDeletionAsync(string firebaseUid, CancellationToken cancellationToken = default)
    {
        var key = Key(firebaseUid);
        if (_cache.TryGetValue(key, out bool pending))
            return pending;

        pending = await _db.Customers.AsNoTracking().AnyAsync(
            c => c.FirebaseUid == firebaseUid && c.DeletionRequestedAt != null && c.DeletedAt == null,
            cancellationToken);

        _cache.Set(key, pending, Ttl);
        return pending;
    }

    public void Invalidate(string firebaseUid) => _cache.Remove(Key(firebaseUid));

    private static string Key(string uid) => "acct-lock:" + uid;
}