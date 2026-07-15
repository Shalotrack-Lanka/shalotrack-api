using System.Security.Cryptography;
using System.Text;

namespace ShaloTrack_API.Middlewares;

public class AdminSyncKeyMiddleware
{
    private readonly RequestDelegate _next;

    public AdminSyncKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration config)
    {
        if (context.Request.Path.StartsWithSegments("/api/internal/customers-sync"))
        {
            var expected = config["AdminSync:Key"];
            var provided = context.Request.Headers["X-Admin-Sync-Key"].ToString();

            var expectedBytes = Encoding.UTF8.GetBytes(expected ?? "");
            var providedBytes = Encoding.UTF8.GetBytes(provided);

            if (string.IsNullOrEmpty(expected)
                || providedBytes.Length != expectedBytes.Length
                || !CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await _next(context);
    }
}