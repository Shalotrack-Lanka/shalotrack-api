using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ShaloTrack_API.Auth;
using ShaloTrack_API.Extensions;
using ShaloTrack_API.Hubs;
using ShaloTrack_API.Middlewares;
using ShaloTrack_API.Services.Implementations;
using ShaloTrack_API.Services.Interfaces;
using ShaloTrack_API.Services.Realtime;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddRepositoryServices();
builder.Services.AddBusinessServices();

// ---- GPS TRIP ARCHIVAL (Phase 3a) ----
builder.Services.AddAwsServices();
builder.Services.AddScoped<ITripArchivalService, TripArchivalService>();

// ---- GPS TRIP ARCHIVAL -- ARCHIVE-THEN-PURGE PIPELINE (Phase 3b) ----
builder.Services.AddSingleton<ITripCloseEventQueue, TripCloseEventQueue>();
builder.Services.AddScoped<ITripPurgeService, TripPurgeService>();
builder.Services.AddHostedService<TripArchivalQueueWorker>();

// ASP.NET Core
builder.Services.AddControllers();
builder.Services.AddSwaggerDocumentation();

// ---- RATE LIMITING ----
// Protects all API endpoints from abuse and cost-scaling attacks.
// Policy details in Extensions/RateLimitingExtensions.cs.
builder.Services.AddShaloTrackRateLimiting();

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

// ---- REAL-TIME PUSH (Option B) ----
builder.Services.AddSignalR();
builder.Services.AddHostedService<LocationNotificationListener>();

// ---- FCM PUSH ----
var firebaseServiceAccountJson = builder.Configuration["Firebase:ServiceAccountJson"]
    ?? throw new InvalidOperationException(
        "Firebase:ServiceAccountJson is not configured. This must be the full " +
        "service account JSON key content, injected via the Firebase__ServiceAccountJson " +
        "environment variable (sourced from AWS SSM -- see SETUP_PART1_CREDENTIALS.md).");

FirebaseApp.Create(new AppOptions
{
    Credential = CredentialFactory.FromJson<ServiceAccountCredential>(firebaseServiceAccountJson)
                                  .ToGoogleCredential()
                                  .CreateScoped("https://www.googleapis.com/auth/firebase.messaging")
});

builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

// ---- ROAD SNAPPING ----
_ = builder.Configuration["GoogleMaps:RoadsApiKey"]
    ?? throw new InvalidOperationException(
        "GoogleMaps:RoadsApiKey is not configured. Must be injected via the " +
        "GoogleMaps__RoadsApiKey environment variable, sourced from AWS SSM.");

builder.Services.AddHttpClient("GoogleRoadsApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ---- GATEWAY COMMAND API ----
// Internal VPC HTTP client for forwarding device commands to the Python gateway.
// Timeout: 10s — commands must complete within this window or are treated as failed.
builder.Services.AddHttpClient("GatewayCommandClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ---- OBSERVABILITY (OTel -> SRE stack) ----
var otelBase = "http://otel.shalotrack.internal:4318";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName: "shalotrack-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.Filter = httpContext => httpContext.Request.Path != "/health";
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(otlp =>
        {
            otlp.Endpoint = new Uri($"{otelBase}/v1/traces");
            otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter(otlp =>
        {
            otlp.Endpoint = new Uri($"{otelBase}/v1/metrics");
            otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
        }));

builder.Logging.AddOpenTelemetry(logging =>
{
    logging.AddOtlpExporter(otlp =>
    {
        otlp.Endpoint = new Uri($"{otelBase}/v1/logs");
        otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
    });
});

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

// SECURITY FIX: Swagger locked to Development only.
// The original code used:
//   if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
// — always true, so Swagger was always public. Fixed: env check is inside
// UseSwaggerDocumentation() and gates on IsDevelopment() only.
app.UseSwaggerDocumentation(app.Environment);

// SECURITY FIX: Rate limiting middleware.
// After ForwardedHeaders + exception handler (infrastructure concerns),
// before auth — abusive requests are dropped before JWT validation runs.
app.UseRateLimiter();

app.UseMiddleware<AdminSyncKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Health check must never be rate-limited — ALB and monitoring hit this
// constantly. DisableRateLimiting() exempts it from the global policy.
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous()
   .DisableRateLimiting();

app.MapControllers();

// SECURITY FIX: SignalR hub now rate-limited on negotiate.
// 20 connection attempts / 60 seconds per IP prevents connection-flood abuse.
app.MapHub<LocationHub>("/hubs/location")
   .RequireRateLimiting(RateLimitingExtensions.Policies.SignalRHub);

app.Run();