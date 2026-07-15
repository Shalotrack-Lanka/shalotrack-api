using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using ShaloTrack_API.Auth;
using ShaloTrack_API.Extensions;
using ShaloTrack_API.Hubs;
using ShaloTrack_API.Services.Realtime;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddRepositoryServices();
builder.Services.AddBusinessServices();

// ASP.NET Core
builder.Services.AddControllers();
builder.Services.AddSwaggerDocumentation();

// ---- AUTH ----
var firebaseProjectId = builder.Configuration["Firebase:ProjectId"]
    ?? throw new InvalidOperationException("Firebase:ProjectId is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
            ValidateAudience = true,
            ValidAudience = firebaseProjectId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        // NEW -- SignalR's WebSocket transport can't set an Authorization header the
        // way a normal HTTP request can, so its clients send the token as a query
        // string parameter instead. This reads it into the same validation pipeline
        // REST endpoints already use, but ONLY for requests to /hubs/*, so normal
        // API routes still require a proper Authorization header.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// ---- REAL-TIME PUSH (NEW) ----
builder.Services.AddSignalR();
builder.Services.AddHostedService<LocationNotificationListener>();

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";

        var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionFeature?.Error;

        var isTimeout = exception?.Message.Contains("Timeout") == true
                        || exception?.InnerException?.Message.Contains("Timeout") == true;

        string? detailed = app.Environment.IsDevelopment()
            ? (exception?.InnerException?.Message ?? exception?.Message)
            : null;

        if (isTimeout)
        {
            context.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
            await context.Response.WriteAsJsonAsync(new
            {
                statusCode = (int)HttpStatusCode.GatewayTimeout,
                message = "Upstream database timeout. Please retry shortly.",
                detailed
            });
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        await context.Response.WriteAsJsonAsync(new
        {
            statusCode = (int)HttpStatusCode.InternalServerError,
            message = "An unexpected error occurred.",
            detailed
        });
    });
});

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwaggerDocumentation();
}

// app.UseHttpsRedirection();  // still deferred, see earlier notes on ALB TLS termination

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

app.MapControllers();

// NEW -- the real-time push endpoint. Android clients connect here after login.
app.MapHub<LocationHub>("/hubs/location");

app.Run();