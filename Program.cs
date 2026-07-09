using Microsoft.AspNetCore.Diagnostics;
using ShaloTrack_API.Extensions;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// Infrastructure
builder.Services.AddDatabaseServices(builder.Configuration);
builder.Services.AddRepositoryServices();
builder.Services.AddBusinessServices();

// ASP.NET Core
builder.Services.AddControllers();
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

// Global Exception Handler for Production & Development Environments
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        context.Response.ContentType = "application/json";

        var exceptionFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionFeature?.Error;

        // Custom SRE Error Response format
        var errorResponse = new
        {
            statusCode = (int)HttpStatusCode.InternalServerError,
            message = "An unhandled error occurred inside the ShaloTrack API server.",
            // Updated to grab the inner exception so you can see the REAL database error
            detailed = (string?)(exception?.InnerException?.Message ?? exception?.Message)
        };

        // If it's a known timeout or database stream issue, clarify it
        if (exception?.Message.Contains("Timeout") == true || exception?.InnerException?.Message.Contains("Timeout") == true)
        {
            context.Response.StatusCode = (int)HttpStatusCode.GatewayTimeout;
            errorResponse = new
            {
                statusCode = (int)HttpStatusCode.GatewayTimeout,
                message = "Database connection pool timeout error.",
                // Added (string?) cast right here to fix your compiler error
                detailed = (string?)"The database pooler took too long to return data. Check Supabase query locks."
            };
        }
        else
        {
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }

        await context.Response.WriteAsJsonAsync(errorResponse);
    });
});

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwaggerDocumentation();
}

// Temporarily disable until ALB is fully configured
//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow
}));

app.MapControllers();

app.Run();