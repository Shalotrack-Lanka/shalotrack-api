using System.Security.Claims;

namespace ShaloTrack_API.Auth;

public interface ICurrentUser
{
    string? FirebaseUid { get; }
    bool IsAuthenticated { get; }
    bool IsStaff { get; }
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

    public bool IsStaff
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user is null) return false;
            return user.IsInRole("Admin") || user.IsInRole("Dealer");
        }
    }
}