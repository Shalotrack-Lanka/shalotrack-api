using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;

namespace ShaloTrack_API.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<ShaloTrackDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsqlOptions =>
                {
                    // PERFORMANCE FIX: Supabase session pooler gives ~15 real
                    // connections per client. EF Core's default MaxPoolSize is
                    // 1024 — a mismatch that causes silent hangs under load as
                    // EF Core believes connections are available while Supabase
                    // has already exhausted the actual limit.
                    //
                    // Set MaxPoolSize to match the real Supabase ceiling so EF
                    // Core queues and waits cleanly instead of hanging.
                    // CommandTimeout: 30s — prevents a slow/locked query from
                    // tying up a connection indefinitely.
                    npgsqlOptions.CommandTimeout(30);
                })
            .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

        // PERFORMANCE FIX: Set Npgsql connection pool max to stay under
        // Supabase's actual session pooler connection limit (~15).
        // Without this, the Npgsql driver can open more connections than
        // Supabase allows, causing new requests to hang waiting for a slot
        // that will never be freed. 10 leaves headroom for the persistent
        // RealtimeConnection used by LocationNotificationListener.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);

        return services;
    }
}