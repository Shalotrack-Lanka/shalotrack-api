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

    /// <summary>
    /// The phone number (E.164) that Firebase holds for <paramref name="uid"/>, i.e. the number our own
    /// OTP proved, or null if the account has none / does not exist. This is the authoritative source for
    /// "which phone did this person verify": never trust a phone number sent in a request body.
    /// </summary>
    Task<string?> GetPhoneNumberAsync(string uid, CancellationToken cancellationToken = default);

    /// <summary>
    /// The uid of the Firebase account that holds <paramref name="e164PhoneNumber"/>, or null if no account does.
    /// Firebase keeps phone numbers unique, so this is the authority on "is this number taken".
    /// </summary>
    Task<string?> FindUidByPhoneNumberAsync(string e164PhoneNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves the account's sign-in number to <paramref name="e164PhoneNumber"/>. Returns false (and changes nothing)
    /// if another Firebase account already holds that number: Firebase enforces this atomically, so two people
    /// racing for one number cannot both win. Only call after the new number has been proved with our own OTP.
    /// </summary>
    Task<bool> SetPhoneNumberAsync(string uid, string e164PhoneNumber, CancellationToken cancellationToken = default);
}
