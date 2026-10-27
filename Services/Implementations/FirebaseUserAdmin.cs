using FirebaseAdmin;
using FirebaseAdmin.Auth;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Uses a SECOND, named FirebaseApp ("auth-admin", created in Program.cs) because the default app is
/// scoped to firebase.messaging only and Firebase would refuse user-management calls with it.
/// </summary>
public class FirebaseUserAdmin : IFirebaseUserAdmin
{
    public const string AppName = "auth-admin";

    private static FirebaseAuth Client => FirebaseAuth.GetAuth(
        FirebaseApp.GetInstance(AppName)
        ?? throw new InvalidOperationException("The 'auth-admin' FirebaseApp is not initialised."));

    public async Task RevokeSessionsAsync(string uid, CancellationToken cancellationToken = default)
    {
        try
        {
            await Client.RevokeRefreshTokensAsync(uid, cancellationToken);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            // Already gone: nothing to revoke.
        }
    }

    public async Task DeleteUserAsync(string uid, CancellationToken cancellationToken = default)
    {
        try
        {
            await Client.DeleteUserAsync(uid, cancellationToken);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            // Already gone: a retry after a partial purge lands here, which is fine.
        }
    }

    public async Task<(string Uid, bool IsNewUser)> GetOrCreateUserByPhoneAsync(
        string e164PhoneNumber, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await Client.GetUserByPhoneNumberAsync(e164PhoneNumber, cancellationToken);
            return (existing.Uid, false);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            // No Firebase account for this number yet -- create one. We just proved ownership of
            // the number ourselves (OTP via text.lk), so this is the one place that's allowed to
            // create a Firebase user without Firebase itself having verified anything.
            var created = await Client.CreateUserAsync(new UserRecordArgs
            {
                PhoneNumber = e164PhoneNumber
            }, cancellationToken);

            return (created.Uid, true);
        }
    }

    public async Task<string> CreateCustomTokenAsync(string uid, CancellationToken cancellationToken = default)
    {
        return await Client.CreateCustomTokenAsync(uid, cancellationToken);
    }
}