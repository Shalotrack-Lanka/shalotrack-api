using Microsoft.OpenApi.Models;

namespace ShaloTrack_API.Extensions;

public static class SwaggerExtensions
{
    // Called unconditionally in builder.Services — Swagger generation metadata
    // is always registered. The middleware method below decides whether to
    // actually expose the UI based on environment.
    public static IServiceCollection AddSwaggerDocumentation(
        this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ShaloTrack API",
                Version = "v1",
                Description = "REST API for the ShaloTrack GPS Tracking Platform."
            });

            // Bearer auth so the Authorize padlock appears in Swagger UI.
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste a Firebase ID token. Just the token — Swagger adds the 'Bearer ' prefix."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });

            var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    // SECURITY FIX: Swagger UI is only served in Development.
    // In Production, /swagger/* returns 404.
    //
    // The original code had:
    //   if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
    // — that condition is always true. Swagger was serving publicly on
    // production the entire time. Fixed by removing the condition and gating
    // inside this method on IsDevelopment() only.
    //
    // Call in Program.cs as:
    //   app.UseSwaggerDocumentation(app.Environment);
    public static IApplicationBuilder UseSwaggerDocumentation(
        this IApplicationBuilder app,
        IWebHostEnvironment env)
    {
        if (!env.IsDevelopment())
        {
            return app;
        }

        app.UseSwagger();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "ShaloTrack API v1");
            options.RoutePrefix = "swagger";
        });

        return app;
    }
}