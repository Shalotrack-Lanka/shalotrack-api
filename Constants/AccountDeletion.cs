namespace ShaloTrack_API.Constants;

/// <summary>Rules and wording for "delete my account" (PDPA / App Store: in-app deletion).</summary>
public static class AccountDeletion
{
    /// <summary>Days between the request and the permanent erasure. The customer can cancel until then.</summary>
    public const int GraceDays = 30;

    /// <summary>What the customer must type to confirm.</summary>
    public const string ConfirmWord = "DELETE";

    /// <summary>The sign-in must be this recent. Fleet/app tokens are minted at sign-in, so this is "signed in within the last N minutes".</summary>
    public static readonly TimeSpan MaxSignInAge = TimeSpan.FromMinutes(10);

    public const string PendingErrorCode = "ACCOUNT_PENDING_DELETION";
    public const string ReauthErrorCode = "REAUTH_REQUIRED";

    public const string RemovedText = "[Removed at the customer's request]";
    public const string DeletedName = "Deleted customer";
    public const string DeletedEmailDomain = "deleted.invalid";
}