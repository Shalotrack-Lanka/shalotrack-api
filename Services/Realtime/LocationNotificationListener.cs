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
/// Holds a persistent Postgres connection and listens on TWO channels:
///   - location_updates      (CurrentLocations writes -- position, live push)
///   - device_status_updates (DeviceStatuses writes -- battery, power, ignition)
///
/// Both are handled over the SAME connection (Postgres LISTEN supports multiple
/// channels per connection) rather than opening a second one.
///
/// Alert detection:
///   - Ignition change: watched from BOTH sources, sharing one in-memory cache
///     keyed by DeviceId, so a change reported by either table only fires once.
///   - Overspeed: from location_updates only, fires once per speeding episode.
///   - Power-cut: from device_status_updates only, fires on Connected->Disconnected.
///   - Low-battery: from device_status_updates only, fires once per "below
///     threshold" episode (not on every single low reading).
///
/// CRITICAL: RealtimeConnection MUST use the session pooler (port 5432), never
/// the transaction pooler. See earlier setup notes.
///
/// KNOWN TRADEOFF: all state is tracked in memory, lost on process restart.
/// Acceptable for now -- a missed alert is low severity, not a safety issue.
/// </summary>
public class LocationNotificationListener : BackgroundService
{
    private readonly string _connectionString;
    private readonly IHubContext<LocationHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LocationNotificationListener> _logger;

    private const decimal OverspeedThresholdKmh = 80m;
    private const int LowBatteryThresholdPercent = 20;

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
                        if (args.Channel == "location_updates")
                        {
                            await HandleLocationNotification(args.Payload);
                        }
                        else if (args.Channel == "device_status_updates")
                        {
                            await HandleDeviceStatusNotification(args.Payload);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling notification on channel {Channel}", args.Channel);
                    }
                };

                await using (var cmd = new NpgsqlCommand("LISTEN location_updates;", connection))
                {
                    await cmd.ExecuteNonQueryAsync(stoppingToken);
                }
                await using (var cmd = new NpgsqlCommand("LISTEN device_status_updates;", connection))
                {
                    await cmd.ExecuteNonQueryAsync(stoppingToken);
                }

                _logger.LogInformation(
                    "LocationNotificationListener: listening on location_updates and device_status_updates.");

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

    // ---- location_updates ----

    private async Task HandleLocationNotification(string payload)
    {
        var data = JsonSerializer.Deserialize<LocationNotificationPayload>(
            payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data?.VehicleId is null) return;

        await _hubContext.Clients
            .Group(data.VehicleId.ToString()!)
            .SendAsync("LocationUpdated", data);

        if (data.DeviceId is not null)
        {
            await CheckLocationAlertsAsync(data);
        }
    }

    private async Task CheckLocationAlertsAsync(LocationNotificationPayload data)
    {
        var deviceId = data.DeviceId!.Value;
        var state = _deviceStates.GetOrAdd(deviceId, _ => new DeviceAlertState());

        var alertsToCreate = new List<Alert>();

        lock (state)
        {
            if (state.IgnitionStatus.HasValue && state.IgnitionStatus.Value != data.IgnitionStatus)
            {
                alertsToCreate.Add(BuildAlert(
                    data.VehicleId!.Value, data.DeviceId, data.Latitude, data.Longitude,
                    data.IgnitionStatus ? AlertType.IgnitionOn : AlertType.IgnitionOff,
                    data.IgnitionStatus ? "Ignition turned on" : "Ignition turned off"));
            }
            state.IgnitionStatus = data.IgnitionStatus;

            bool isSpeeding = data.Speed > OverspeedThresholdKmh;
            if (isSpeeding && !state.IsSpeeding)
            {
                alertsToCreate.Add(BuildAlert(
                    data.VehicleId!.Value, data.DeviceId, data.Latitude, data.Longitude,
                    AlertType.Overspeed,
                    $"Speed exceeded {OverspeedThresholdKmh} km/h (reached {data.Speed:F0} km/h)"));
            }
            state.IsSpeeding = isSpeeding;
        }

        if (alertsToCreate.Count > 0)
        {
            await PersistAlertsAsync(alertsToCreate, data.VehicleId!.Value);
        }
    }

    // ---- device_status_updates ----

    private async Task HandleDeviceStatusNotification(string payload)
    {
        var data = JsonSerializer.Deserialize<DeviceStatusNotificationPayload>(
            payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data?.VehicleId is null || data.DeviceId is null) return;

        await CheckDeviceStatusAlertsAsync(data);
    }

    private async Task CheckDeviceStatusAlertsAsync(DeviceStatusNotificationPayload data)
    {
        var deviceId = data.DeviceId!.Value;
        var state = _deviceStates.GetOrAdd(deviceId, _ => new DeviceAlertState());

        var alertsToCreate = new List<Alert>();

        lock (state)
        {
            if (state.IgnitionStatus.HasValue && state.IgnitionStatus.Value != data.IgnitionStatus)
            {
                alertsToCreate.Add(BuildAlert(
                    data.VehicleId!.Value, data.DeviceId, null, null,
                    data.IgnitionStatus ? AlertType.IgnitionOn : AlertType.IgnitionOff,
                    data.IgnitionStatus ? "Ignition turned on" : "Ignition turned off"));
            }
            state.IgnitionStatus = data.IgnitionStatus;

            // Power-cut: PowerStatus 0 = Connected, 1 = Disconnected.
            bool isDisconnected = data.PowerStatus == 1;
            if (isDisconnected && state.PowerStatus is not true)
            {
                alertsToCreate.Add(BuildAlert(
                    data.VehicleId!.Value, data.DeviceId, null, null,
                    AlertType.PowerCut,
                    "Device power disconnected"));
            }
            state.PowerStatus = isDisconnected;

            bool isLowBattery = data.BatteryLevel < LowBatteryThresholdPercent;
            if (isLowBattery && !state.IsLowBattery)
            {
                alertsToCreate.Add(BuildAlert(
                    data.VehicleId!.Value, data.DeviceId, null, null,
                    AlertType.LowBattery,
                    $"Battery level dropped below {LowBatteryThresholdPercent}% (currently {data.BatteryLevel}%)"));
            }
            state.IsLowBattery = isLowBattery;
        }

        if (alertsToCreate.Count > 0)
        {
            await PersistAlertsAsync(alertsToCreate, data.VehicleId!.Value);
        }
    }

    // ---- shared persistence ----

    private async Task PersistAlertsAsync(List<Alert> alerts, Guid vehicleId)
    {
        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        foreach (var alert in alerts)
        {
            await unitOfWork.Alerts.AddAsync(alert);
        }
        await unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created {Count} alert(s) for vehicle {VehicleId}", alerts.Count, vehicleId);
    }

    private static Alert BuildAlert(
        Guid vehicleId, Guid? deviceId, decimal? latitude, decimal? longitude,
        AlertType type, string message)
    {
        return new Alert
        {
            VehicleId = vehicleId,
            DeviceId = deviceId,
            AlertType = type,
            Message = message,
            Latitude = latitude,
            Longitude = longitude,
            TriggeredAt = DateTime.UtcNow,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    private class DeviceAlertState
    {
        public bool? IgnitionStatus { get; set; }
        public bool IsSpeeding { get; set; }
        public bool? PowerStatus { get; set; }
        public bool IsLowBattery { get; set; }
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

public class DeviceStatusNotificationPayload
{
    public Guid? VehicleId { get; set; }
    public Guid? DeviceId { get; set; }
    public int BatteryLevel { get; set; }
    public int PowerStatus { get; set; }
    public bool IgnitionStatus { get; set; }
    public bool IsOnline { get; set; }
    public DateTime UpdatedAt { get; set; }
}