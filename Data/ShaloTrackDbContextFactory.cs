using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ShaloTrack_API.Data;

/// <summary>
/// Lets EF Core's CLI tools (migrations add, database update, etc.) build the
/// DbContext directly, without running the full Program.cs startup pipeline.
/// EF Core automatically discovers and uses this factory at design time --
/// no registration needed, just having this class exist in the project.
///
/// This is what fixes "Firebase:ServiceAccountJson is not configured" errors
/// when running EF tooling locally: previously, scaffolding a migration
/// required building the whole app (including Program.cs's Firebase
/// initialization) just to construct a DbContext that has nothing to do with
/// Firebase at all. This factory sidesteps that entirely -- production still
/// requires real Firebase credentials to actually run (that fail-loud
/// behavior is untouched), but local migration tooling never touches it.
/// </summary>
public class ShaloTrackDbContextFactory : IDesignTimeDbContextFactory<ShaloTrackDbContext>
{
    public ShaloTrackDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No DefaultConnection string found in appsettings.json, " +
                "appsettings.Development.json, or the ConnectionStrings__DefaultConnection " +
                "environment variable. This is needed to scaffold migrations locally " +
                "(it doesn't need to be a live/reachable connection for 'migrations add', " +
                "just a valid Postgres connection string format)."
            );
        }

        var optionsBuilder = new DbContextOptionsBuilder<ShaloTrackDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ShaloTrackDbContext(optionsBuilder.Options);
    }
}