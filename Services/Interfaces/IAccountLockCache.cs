namespace ShaloTrack_API.Services.Interfaces;

/// <summary>Fast "is this signed-in user waiting for account deletion?" check used on every request.</summary>
public interface IAccountLockCache
{
    Task<bool> IsPendingDeletionAsync(string firebaseUid, CancellationToken cancellationToken = default);

    /// <summary>Call after the state changes (request / cancel) so the change takes effect immediately.</summary>
    void Invalidate(string firebaseUid);
}