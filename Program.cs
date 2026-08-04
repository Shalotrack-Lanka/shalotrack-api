using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder.Extensions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
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

// ASP.NET Core
builder.Services.AddControllers();
builder.Services.AddSwaggerDocumentation();

// ---- AUTH (was entirely missing) ----
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

        // NEW -- SignalR's WebSocket transport can't set an Authorization header,
        // so its clients send the token as a query string parameter instead. Only
        // applies to /hubs/* paths -- normal REST routes still require a proper
        // Authorization header.
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

// ---- FCM PUSH (NEW) ----
var firebaseServiceAccountJson = builder.Configuration["Firebase:ServiceAccountJson"]
    ?? throw new InvalidOperationException(
        "Firebase:ServiceAccountJson is not configured. This must be the full " +
        "service account JSON key content, injected via the Firebase__ServiceAccountJson " +
        "environment variable (sourced from AWS SSM -- see SETUP_PART1_CREDENTIALS.md).");

FirebaseApp.Create(new AppOptions
{
    Credential = GoogleCredential.FromJson(firebaseServiceAccountJson)
});

builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();

// ---- ROAD SNAPPING (NEW) ----
// Fail-fast at startup, matching the Firebase:ServiceAccountJson pattern
// above -- a missing key should crash the app at boot, not surface as a
// silent 500 on the first live snap-to-road request. Deliberately a
// SEPARATE key from the Android app's own Maps SDK key: that one is
// Android-app-restricted and rejects these server-side calls, and this
// key must never be sent to the Android app at all regardless.
_ = builder.Configuration["GoogleMaps:RoadsApiKey"]
    ?? throw new InvalidOperationException(
        "GoogleMaps:RoadsApiKey is not configured. Must be injected via the " +
        "GoogleMaps__RoadsApiKey environment variable, sourced from AWS SSM.");

builder.Services.AddHttpClient("GoogleRoadsApi", client =>
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
            // Don't let ALB health-check pings flood your traces every 30s
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

        // FIX: only expose internal error text in Development.
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

// Re-enable once ALB TLS is confirmed:
// app.UseHttpsRedirection();

// Interim fix (2026-07-15): lets the Laravel Admin sync job call
// /api/internal/customers-sync with a shared key instead of a Firebase token.
// Must run before UseAuthentication() or Firebase blocks it first.
// TODO: replace with a Firebase service-account token (see chat) and remove this
// once the key is moved to AWS SSM + the route is restricted to VPC-internal traffic.
app.UseMiddleware<AdminSyncKeyMiddleware>();

// ORDER MATTERS: authentication before authorization.
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow }))
   .AllowAnonymous();

app.MapControllers();

// NEW -- the real-time push endpoint. Android clients connect here after login.
app.MapHub<LocationHub>("/hubs/location");

app.Run();