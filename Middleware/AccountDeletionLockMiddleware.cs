using ShaloTrack_API.Auth;
using ShaloTrack_API.Constants;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Middlewares;

/// <summary>
/// While an account is waiting for deletion, the signed-in customer can reach only api/Account/*
/// (see status, download data, cancel). Everything else answers 403 ACCOUNT_PENDING_DELETION, so a
/// token that is still valid for up to an hour after the request cannot keep using the service.
/// Runs after authentication. Staff, internal (sync-key) calls and anonymous requests pass through.
/// </summary>
public class AccountDeletionLockMiddleware
{
    private readonly RequestDelegate _next;

    public AccountDeletionLockMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ICurrentUser user, IAccountLockCache locks)
    {
        var uid = user.FirebaseUid;

        if (!user.IsAuthenticated
            || string.IsNullOrEmpty(uid)
            || user.IsStaff
            || context.Request.Path.StartsWithSegments("/api/account", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (await locks.IsPendingDeletionAsync(uid, context.RequestAborted))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail(
                StatusCodes.Status403Forbidden,
                "Your account is scheduled for deletion. Cancel the deletion to keep using ShaloTrack.",
                AccountDeletion.PendingErrorCode));
            return;
        }

        await _next(context);
    }
}