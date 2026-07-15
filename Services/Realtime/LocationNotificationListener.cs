using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using ShaloTrack_API.Hubs;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Holds a persistent Postgres connection, runs LISTEN location_updates, and pushes
/// each notification to the matching SignalR group.
///
/// CRITICAL: the connection string this uses (ConnectionStrings:RealtimeConnection)
/// MUST use Supabase's SESSION pooler (port 5432), never the transaction pooler
/// (port 6543). LISTEN/NOTIFY requires a stable, persistent session -- transaction
/// pooling recycles the underlying connection between statements and breaks this
/// outright, not just slowly. This is the exact class of misconfiguration that
/// caused a production outage earlier in this project (dashboard endpoint hanging
/// on port 6543) -- keep this connection string separate and explicit so it can
/// never silently inherit the wrong port from another config value.
/// </summary>
public class LocationNotificationListener : BackgroundService
{
    private readonly string _connectionString;
    private readonly IHubContext<LocationHub> _hubContext;
    private readonly ILogger<LocationNotificationListener> _logger;

    public LocationNotificationListener(
        IConfiguration configuration,
        IHubContext<LocationHub> hubContext,
        ILogger<LocationNotificationListener> logger)
    {
        _connectionString = configuration.GetConnectionString("RealtimeConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:RealtimeConnection is not configured. " +
                "It must point to the Supabase SESSION pooler (port 5432).");
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync(stoppingToken);

                connection.Notification += async (sender, args) =>
                {
                    try
                    {
                        await HandleNotification(args.Payload);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling location notification");
                    }
                };

                await using (var cmd = new NpgsqlCommand("LISTEN location_updates;", connection))
                {
                    await cmd.ExecuteNonQueryAsync(stoppingToken);
                }

                _logger.LogInformation("LocationNotificationListener: listening on location_updates.");

                // Blocks here, waking up whenever a notification arrives, until the
                // connection drops or the service is stopped.
                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LocationNotificationListener: connection lost, retrying in 5s.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task HandleNotification(string payload)
    {
        var data = JsonSerializer.Deserialize<LocationNotificationPayload>(
            payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data?.VehicleId is null) return;

        await _hubContext.Clients
            .Group(data.VehicleId.ToString()!)
            .SendAsync("LocationUpdated", data);
    }
}

public class LocationNotificationPayload
{
    public Guid? VehicleId { get; set; }
    public Guid? DeviceId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal Speed { get; set; }
    public decimal Heading { get; set; }
    public bool IgnitionStatus { get; set; }
    public bool MovementStatus { get; set; }
    public DateTime LastUpdate { get; set; }
}