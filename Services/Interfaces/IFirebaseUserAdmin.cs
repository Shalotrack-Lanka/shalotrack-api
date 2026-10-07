namespace ShaloTrack_API.Services.Interfaces;

/// <summary>Firebase Authentication admin actions. "User not found" counts as success (idempotent).</summary>
public interface IFirebaseUserAdmin
{
    /// <summary>Signs the user out everywhere (refresh tokens revoked). Existing ID tokens live out their hour.</summary>
    Task RevokeSessionsAsync(string uid, CancellationToken cancellationToken = default);

    /// <summary>Permanently removes the Firebase account (phone number, sign-in).</summary>
    Task DeleteUserAsync(string uid, CancellationToken cancellationToken = default);
}