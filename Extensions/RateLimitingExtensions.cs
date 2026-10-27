using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace ShaloTrack_API.Extensions;

/// <summary>
/// Production rate limiting using ASP.NET Core 8's built-in RateLimiter.
/// No extra NuGet packages required.
///
/// POLICIES:
///
///   "general_api"    — Applied globally to all API routes via UseRateLimiter().
///                      100 requests / 60 seconds, partitioned per Firebase UID.
///                      A customer polling live location every 10s = 6 req/min —
///                      well under the cap even with multiple open screens.
///
///   "auth_sensitive" — 20 requests / 60 seconds per IP.
///                      Applied to: POST /api/Customers, POST /api/Alerts/register-token.
///                      Tighter because these are the highest-value abuse targets.
///
///   "signalr_hub"    — 20 negotiate attempts / 60 seconds per IP.
///                      Applied to: MapHub<LocationHub>("/hubs/location").
///                      Prevents connection-flood abuse against the hub.
///
/// 429 RESPONSE:
///   Returns a standard ApiResponse<T>-shaped envelope so the Android app
///   and admin portal handle it identically to any other error response.
///   Retry-After header is set so clients can back off correctly.
/// </summary>
public static class RateLimitingExtensions
{
    public static class Policies
    {
        public const string GeneralApi = "general_api";
        public const string AuthSensitive = "auth_sensitive";
        public const string SignalRHub = "signalr_hub";
        public const string PublicLive = "public_live";
        public const string AccountExport = "account_export";
        public const string AccountDelete = "account_delete";

        // Custom OTP auth (2026-10-27) -- replaces Firebase Phone Auth's own SMS delivery,
        // which several carriers were silently dropping. These are the entry point to the
        // whole app with no token yet, so they get the tightest buckets in the API: free SMS
        // cost on one side, account-takeover guesses on the other.
        public const string OtpRequest = "otp_request";
        public const string OtpVerify = "otp_verify";
    }

    public static IServiceCollection AddShaloTrackRateLimiting(
        this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // Global 429 handler — every rejected request hits this.
            // Returns the ApiResponse envelope shape so every client handles
            // it uniformly (not a plain text "Too Many Requests").
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)retryAfter.TotalSeconds).ToString();
                }

                var body = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    statusCode = 429,
                    message = "Too many requests. Please slow down and retry.",
                    data = (object?)null,
                    errors = new[] { "Rate limit exceeded." },
                    timestamp = DateTime.UtcNow
                });

                await context.HttpContext.Response.WriteAsync(body, cancellationToken);
            };

            // 100 req / 60 s per Firebase UID.
            // Falls back to IP when called without a valid JWT (auth middleware
            // rejects it anyway, but the limiter runs first and needs a key).
            options.AddPolicy(Policies.GeneralApi, httpContext =>
            {
                var uid = httpContext.User?.FindFirst("sub")?.Value
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter(uid, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromSeconds(60),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // 20 req / 60 s per IP — tighter bucket for sensitive endpoints.
            options.AddPolicy(Policies.AuthSensitive, httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(ip, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromSeconds(60),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // Public live-share page: 30 req / 60 s PER LINK (the page polls about every 10 s, i.e. 6/min).
            // Partitioned by the token in the path, not by IP: every viewer reaches the API through the
            // Fleet server, so an IP partition would put all viewers in one bucket. Malformed tokens share
            // one bucket so garbage cannot create unlimited partitions.
            options.AddPolicy(Policies.PublicLive, httpContext =>
            {
                var token = httpContext.Request.RouteValues["token"] as string;
                var key = token is { Length: 43 } ? token : "malformed";

                return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromSeconds(60),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // Data export is heavy (about 15 queries) and rarely needed: 3 per hour per account.
            options.AddPolicy(Policies.AccountExport, httpContext =>
            {
                var uid = httpContext.User?.FindFirst("sub")?.Value
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter("export:" + uid, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3,
                        Window = TimeSpan.FromHours(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // Deleting / cancelling an account is rare and serious: 5 per hour per account.
            options.AddPolicy(Policies.AccountDelete, httpContext =>
            {
                var uid = httpContext.User?.FindFirst("sub")?.Value
                          ?? httpContext.Connection.RemoteIpAddress?.ToString()
                          ?? "anonymous";

                return RateLimitPartition.GetFixedWindowLimiter("acct-delete:" + uid, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromHours(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // 5 OTP SMS requests / 15 min per IP. PhoneVerificationService adds its own
            // per-phone-number cap (5/hour) on top -- this IP bucket is what stops one attacker
            // from cycling through many numbers, the per-number cap is what stops many IPs
            // (a VPN, a botnet) targeting one number.
            options.AddPolicy(Policies.OtpRequest, httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter("otp-req:" + ip, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(15),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // 10 verify attempts / 5 min per IP. Looser than the request bucket (genuine typos
            // happen) but still bounds brute-force guessing against the 6-digit code -- combined
            // with the 5-attempt-per-code lockout in PhoneVerificationService, this closes off
            // credential stuffing from a single source even across many requested codes.
            options.AddPolicy(Policies.OtpVerify, httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter("otp-verify:" + ip, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(5),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            // 20 negotiate attempts / 60 s per IP for the SignalR hub.
            options.AddPolicy(Policies.SignalRHub, httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(ip, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromSeconds(60),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });
        });

        return services;
    }
}