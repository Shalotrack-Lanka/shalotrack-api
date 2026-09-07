using System.Security.Claims;

namespace ShaloTrack_API.Auth;

public interface ICurrentUser
{
    string? FirebaseUid { get; }
    bool IsAuthenticated { get; }
    bool IsStaff { get; }
    bool IsEmailVerified { get; }
}

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _http;

    public CurrentUser(IHttpContextAccessor http) => _http = http;

    public bool IsAuthenticated =>
        _http.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public string? FirebaseUid
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user is null) return null;
            return user.FindFirstValue("user_id")
                ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub");
        }
    }

    // NEW -- real, severe gap found during the pre-launch auth review.
    // Standard Firebase ID token claim, present on every token
    // regardless of scope; reads it directly off the already-validated
    // JWT rather than making a separate Admin SDK call (which would
    // need a broader OAuth scope than the existing FirebaseApp instance
    // has -- that one is scoped only for firebase.messaging, used by
    // PushNotificationService, and changing its scope risked breaking
    // that working code for a change unrelated to it). Fails closed
    // (false) if the claim is missing or unparseable, since this is a
    // security check, not a display concern.
    public bool IsEmailVerified
    {
        get
        {
            var user = _http.HttpContext?.User;
            var raw = user?.FindFirstValue("email_verified");
            return bool.TryParse(raw, out var verified) && verified;
        }
    }

    public bool IsStaff
    {
        get
        {
            if (_http.HttpContext?.Items["IsInternalTrustedRequest"] is true)
                return true;

            var user = _http.HttpContext?.User;
            if (user is null) return false;
            return user.IsInRole("Admin") || user.IsInRole("Dealer");
        }
    }
}