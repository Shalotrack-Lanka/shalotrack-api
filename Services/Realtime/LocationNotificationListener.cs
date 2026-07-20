using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Hubs;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Holds a persistent Postgres connection, runs LISTEN location_updates, pushes each
/// notification to the matching SignalR group, AND (new) checks each update for
/// alert-worthy state changes -- ignition on/off, overspeed -- persisting an Alert
/// row when one fires.
///
/// CRITICAL: the connection string this uses (ConnectionStrings:RealtimeConnection)
/// MUST use Supabase's SESSION pooler (port 5432), never the transaction pooler
/// (port 6543). See earlier setup notes -- this is the same class of misconfiguration
/// that caused a production outage earlier in this project.
///
/// KNOWN TRADEOFF: ignition-change and overspeed-episode detection rely on an
/// in-memory cache of each device's last-seen state, not a persisted one. This is
/// lost on process restart -- a genuine transition happening right at restart could
/// be missed (the next notification after restart has nothing to compare against,
/// so no alert fires for that specific transition). Acceptable for now: a missed
/// alert is a low-severity failure, not a safety issue. If this becomes a real
/// problem in practice, the fix is to persist last-known state to the database
/// instead of memory.
/// </summary>
public class LocationNotificationListener : BackgroundService
{
    private readonly string _connectionString;
    private readonly IHubContext<LocationHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LocationNotificationListener> _logger;

    private const decimal OverspeedThresholdKmh = 80m;

    // Keyed by DeviceId -- see "KNOWN TRADEOFF" above.
    private readonly ConcurrentDictionary<Guid, DeviceAlertState> _deviceStates = new();

    public LocationNotificationListener(
        IConfiguration configuration,
        IHubContext<LocationHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ILogger<LocationNotificationListener> logger)
    {
        _connectionString = configuration.GetConnectionString("RealtimeConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:RealtimeConnection is not configured. " +
                "It must point to the Supabase SESSION pooler (port 5432).");
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
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

        // NEW -- alert trigger detection, on top of the existing push.
        if (data.DeviceId is not null)
        {
            await CheckForAlertsAsync(data);
        }
    }

    private async Task CheckForAlertsAsync(LocationNotificationPayload data)
    {
        var deviceId = data.DeviceId!.Value;
        _deviceStates.TryGetValue(deviceId, out var previous);

        var alertsToCreate = new List<Alert>();

        // Ignition change -- only fires when we have a previous reading to compare
        // against (the very first sighting of a device is never itself a "change").
        if (previous is not null && previous.IgnitionStatus != data.IgnitionStatus)
        {
            alertsToCreate.Add(BuildAlert(
                data,
                data.IgnitionStatus ? AlertType.IgnitionOn : AlertType.IgnitionOff,
                data.IgnitionStatus ? "Ignition turned on" : "Ignition turned off"));
        }

        // Overspeed -- fires once per speeding episode (transition into speeding),
        // not on every single update while still over the threshold.
        bool isSpeeding = data.Speed > OverspeedThresholdKmh;
        bool wasSpeeding = previous?.IsSpeeding ?? false;
        if (isSpeeding && !wasSpeeding)
        {
            alertsToCreate.Add(BuildAlert(
                data,
                AlertType.Overspeed,
                $"Speed exceeded {OverspeedThresholdKmh} km/h (reached {data.Speed:F0} km/h)"));
        }

        _deviceStates[deviceId] = new DeviceAlertState
        {
            IgnitionStatus = data.IgnitionStatus,
            IsSpeeding = isSpeeding
        };

        if (alertsToCreate.Count == 0) return;

        // BackgroundService runs at singleton scope; IUnitOfWork is scoped, so a
        // fresh scope is created per notification that actually needs to write.
        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        foreach (var alert in alertsToCreate)
        {
            await unitOfWork.Alerts.AddAsync(alert);
        }
        await unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Created {Count} alert(s) for vehicle {VehicleId}",
            alertsToCreate.Count, data.VehicleId);
    }

    private static Alert BuildAlert(LocationNotificationPayload data, AlertType type, string message)
    {
        return new Alert
        {
            VehicleId = data.VehicleId!.Value,
            DeviceId = data.DeviceId,
            AlertType = type,
            Message = message,
            Latitude = data.Latitude,
            Longitude = data.Longitude,
            TriggeredAt = DateTime.UtcNow,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    private class DeviceAlertState
    {
        public bool IgnitionStatus { get; set; }
        public bool IsSpeeding { get; set; }
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