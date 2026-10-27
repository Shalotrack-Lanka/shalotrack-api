namespace ShaloTrack_API.Services.Interfaces;

/// <summary>Firebase Authentication admin actions. "User not found" counts as success (idempotent).</summary>
public interface IFirebaseUserAdmin
{
    /// <summary>Signs the user out everywhere (refresh tokens revoked). Existing ID tokens live out their hour.</summary>
    Task RevokeSessionsAsync(string uid, CancellationToken cancellationToken = default);

    /// <summary>Permanently removes the Firebase account (phone number, sign-in).</summary>
    Task DeleteUserAsync(string uid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up the Firebase user for <paramref name="e164PhoneNumber"/>, creating one if none
    /// exists. Called only after our own OTP (text.lk) has verified the number, so Firebase never
    /// sees or needs to send an SMS itself. Returns the uid and whether it was just created.
    /// </summary>
    Task<(string Uid, bool IsNewUser)> GetOrCreateUserByPhoneAsync(string e164PhoneNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mints a short-lived Firebase custom token for <paramref name="uid"/>. The client exchanges
    /// this for a normal Firebase ID token via signInWithCustomToken -- nothing downstream of that
    /// point (JwtBearer validation, CurrentUser, FCM) changes.
    /// </summary>
    Task<string> CreateCustomTokenAsync(string uid, CancellationToken cancellationToken = default);
}