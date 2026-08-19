using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Hubs;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Services.Interfaces;

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
/// Place-visit detection (NEW): from location_updates only, same
/// transition-detection shape as the alerts above -- ENTER is detected when
/// a device moves from "not within any saved place's radius" to "within
/// one", not on every ping while continuously parked there. Deliberately
/// inline here, not queued to a background worker like Trip Archival is --
/// unlike archival (S3 I/O, bulk deletes), a proximity check + counter
/// increment is cheap and belongs with the other lightweight per-position
/// derived-state checks already living in this class.
///
/// CRITICAL: RealtimeConnection MUST use the session pooler (port 5432), never
/// the transaction pooler. See earlier setup notes.
///
/// State seeding: in-memory state is rebuilt from DeviceStatuses before the
/// first LISTEN and again after every reconnect (not just on process restart --
/// a transient connection drop has the exact same blind spot, since any
/// DeviceStatuses update during the gap never fires a NOTIFY this listener
/// saw). Seeding reads via IDeviceStatusRepository directly, NOT IUnitOfWork --
/// see Phase 2 postmortem for why (IUnitOfWork.DeviceStatuses was declared but
/// never wired, null at runtime, took the whole listener down until fixed).
/// Place-visit "nearby" state is NOT seeded on reconnect -- a missed exit/
/// re-entry pair across a reconnect is a low-severity, self-correcting gap
/// (the next real exit+entry cycle re-syncs it), not worth the extra
/// complexity of persisting/reloading transient proximity state.
///
/// Trip archival (Phase 3b): the moment IgnitionOff is detected -- from
/// EITHER detection path below, not just one -- a TripCloseEvent is enqueued
/// to ITripCloseEventQueue. Both CheckLocationAlertsAsync and
/// CheckDeviceStatusAlertsAsync enqueue on this transition, because whichever
/// one happens to observe the change first is the one that will see
/// state.IgnitionStatus differ; the other arrives after and correctly sees no
/// change. Wiring only one path would silently miss every trip whose closing
/// transition happened to be detected via the other one first.
///
/// KNOWN REMAINING GAP: IsSpeeding is not seeded, since DeviceStatuses carries
/// no speed field -- only location_updates does. A missed overspeed alert
/// across a restart/reconnect is still possible. Low severity, not addressed
/// here.
/// </summary>
public class LocationNotificationListener : BackgroundService
{
    private readonly string _connectionString;
    private readonly IHubContext<LocationHub> _hubContext;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITripCloseEventQueue _tripCloseEventQueue; // NEW -- Phase 3b
    private readonly ILogger<LocationNotificationListener> _logger;

    private const decimal OverspeedThresholdKmh = 80m;
    private const int LowBatteryThresholdPercent = 20;

    private readonly ConcurrentDictionary<Guid, DeviceAlertState> _deviceStates = new();

    public LocationNotificationListener(
        IConfiguration configuration,
        IHubContext<LocationHub> hubContext,
        IServiceScopeFactory scopeFactory,
        ITripCloseEventQueue tripCloseEventQueue, // NEW
        ILogger<LocationNotificationListener> logger)
    {
        _connectionString = configuration.GetConnectionString("RealtimeConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:RealtimeConnection is not configured. " +
                "It must point to the Supabase SESSION pooler (port 5432).");
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
        _tripCloseEventQueue = tripCloseEventQueue; // NEW
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

                // Rebuild in-memory state from the DB BEFORE registering the
                // notification handler or issuing LISTEN, so nothing arriving
                // right after connect can race against a half-seeded cache.
                // Runs on first startup AND on every reconnect.
                await SeedDeviceStatesFromDatabaseAsync();

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

    // ---- state seeding (startup + every reconnect) ----

    private async Task SeedDeviceStatesFromDatabaseAsync()
    {
        using var scope = _scopeFactory.CreateScope();

        var deviceStatusRepository = scope.ServiceProvider.GetRequiredService<IDeviceStatusRepository>();
        var statuses = await deviceStatusRepository.GetAllAsync();

        foreach (var status in statuses)
        {
            var state = _deviceStates.GetOrAdd(status.DeviceId, _ => new DeviceAlertState());

            lock (state)
            {
                state.IgnitionStatus = status.IgnitionStatus;
                state.PowerStatus = (int)status.PowerStatus == 1;
                state.IsLowBattery = status.BatteryLevel < LowBatteryThresholdPercent;
            }
        }

        _logger.LogInformation(
            "LocationNotificationListener: seeded state for {Count} device(s) from DeviceStatuses.",
            statuses.Count);
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
            await CheckPlaceVisitsAsync(data); // NEW
        }
    }

    private async Task CheckLocationAlertsAsync(LocationNotificationPayload data)
    {
        var deviceId = data.DeviceId!.Value;
        var state = _deviceStates.GetOrAdd(deviceId, _ => new DeviceAlertState());

        var alertsToCreate = new List<Alert>();
        var tripJustClosed = false; // NEW

        lock (state)
        {
            if (state.IgnitionStatus.HasValue && state.IgnitionStatus.Value != data.IgnitionStatus)
            {
                alertsToCreate.Add(BuildAlert(
                    data.VehicleId!.Value, data.DeviceId, data.Latitude, data.Longitude,
                    data.IgnitionStatus ? AlertType.IgnitionOn : AlertType.IgnitionOff,
                    data.IgnitionStatus ? "Ignition turned on" : "Ignition turned off"));

                // NEW -- Phase 3b: trip close detected via the location_updates
                // path. See class summary for why this mirrors the same check
                // in CheckDeviceStatusAlertsAsync rather than relying on only one.
                if (!data.IgnitionStatus)
                {
                    tripJustClosed = true;
                }
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

        if (tripJustClosed)
        {
            _tripCloseEventQueue.Enqueue(new TripCloseEvent(deviceId, data.VehicleId!.Value, DateTime.UtcNow));
        }

        if (alertsToCreate.Count > 0)
        {
            await PersistAlertsAsync(alertsToCreate, data.VehicleId!.Value);
        }
    }

    // ---- place-visit detection (NEW) ----

    // Same shape as CheckLocationAlertsAsync: in-memory per-device state,
    // transition detected under lock, DB work done outside the lock. Only
    // acts on ENTER transitions (newly within a place's radius), so a
    // vehicle parked at a saved place for hours only counts as one visit,
    // not one per GPS ping.
    private async Task CheckPlaceVisitsAsync(LocationNotificationPayload data)
    {
        var deviceId = data.DeviceId!.Value;
        var vehicleId = data.VehicleId!.Value;

        using var scope = _scopeFactory.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId);
        if (vehicle is null) return;

        var places = await unitOfWork.SavedPlaces.GetByCustomerAsync(vehicle.CustomerId);
        if (places.Count == 0) return;

        var state = _deviceStates.GetOrAdd(deviceId, _ => new DeviceAlertState());

        List<Guid> newlyEnteredPlaceIds;
        lock (state)
        {
            var currentlyNearby = new HashSet<Guid>();
            foreach (var place in places)
            {
                double distanceMeters = HaversineDistanceMeters(
                    (double)data.Latitude, (double)data.Longitude,
                    (double)place.Latitude, (double)place.Longitude);

                if (distanceMeters <= place.RadiusMeters)
                {
                    currentlyNearby.Add(place.PlaceId);
                }
            }

            newlyEnteredPlaceIds = currentlyNearby.Except(state.NearbyPlaceIds).ToList();
            state.NearbyPlaceIds = currentlyNearby;
        }

        foreach (var placeId in newlyEnteredPlaceIds)
        {
            // Re-fetched individually (tracked, unlike the AsNoTracking list
            // above) so this update is safe to save directly.
            var trackedPlace = await unitOfWork.SavedPlaces.GetByIdAsync(placeId);
            if (trackedPlace is null) continue;

            trackedPlace.VisitCount += 1;
            trackedPlace.LastVisitedAt = DateTime.UtcNow;
        }

        if (newlyEnteredPlaceIds.Count > 0)
        {
            await unitOfWork.SaveChangesAsync();
            _logger.LogInformation(
                "Recorded {Count} place visit(s) for vehicle {VehicleId}", newlyEnteredPlaceIds.Count, vehicleId);
        }
    }

    // Standard great-circle distance between two lat/lng points, in meters.
    private static double HaversineDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6371000;
        double dLat = ToRadians(lat2 - lat1);
        double dLon = ToRadians(lon2 - lon1);
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return earthRadiusMeters * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;

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
        var tripJustClosed = false; // NEW

        lock (state)
        {
            if (state.IgnitionStatus.HasValue && state.IgnitionStatus.Value != data.IgnitionStatus)
            {
                alertsToCreate.Add(BuildAlert(
                    data.VehicleId!.Value, data.DeviceId, null, null,
                    data.IgnitionStatus ? AlertType.IgnitionOn : AlertType.IgnitionOff,
                    data.IgnitionStatus ? "Ignition turned on" : "Ignition turned off"));

                // NEW -- Phase 3b: trip close detected via the device_status_updates
                // path. See class summary for why this mirrors the same check in
                // CheckLocationAlertsAsync rather than relying on only one.
                if (!data.IgnitionStatus)
                {
                    tripJustClosed = true;
                }
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

        if (tripJustClosed)
        {
            _tripCloseEventQueue.Enqueue(new TripCloseEvent(deviceId, data.VehicleId!.Value, DateTime.UtcNow));
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

        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId);
        if (vehicle is null)
        {
            _logger.LogWarning("Could not resolve customer for vehicle {VehicleId}, skipping push.", vehicleId);
            return;
        }

        var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();

        // NEW -- Vehicle Sharing. Alerts now also reach every customer
        // with an Accepted share for this vehicle, not just the owner.
        // Resolved once per vehicle (not per alert) since the share list
        // doesn't change between alerts in the same batch.
        var acceptedShares = await unitOfWork.VehicleShares.GetAcceptedSharesForVehicleAsync(vehicleId);

        foreach (var alert in alerts)
        {
            string title = $"{vehicle.VehicleNumber}: {alert.AlertType}";

            // FIX: this call was never wrapped -- if it throws for any
            // reason, the exception would propagate up through this
            // background listener with unclear consequences, even though
            // the alert itself was already successfully persisted above.
            // Same lesson as the real SOS 500 incident: a push failure
            // must never risk an already-successful core action.
            try
            {
                await pushService.SendAlertPushAsync(vehicle.CustomerId, title, alert.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Alert push failed for owner {CustomerId}, vehicle {VehicleId} -- alert was already persisted.", vehicle.CustomerId, vehicleId);
            }

            foreach (var share in acceptedShares)
            {
                try
                {
                    await pushService.SendAlertPushAsync(share.SharedWithCustomerId, title, alert.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Alert push failed for shared viewer {CustomerId}, vehicle {VehicleId} -- alert was already persisted.", share.SharedWithCustomerId, vehicleId);
                }
            }
        }
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

        // NEW -- which of this device's owner's saved places it was within
        // radius of, as of the last check. Compared against the current
        // check to detect ENTER transitions (see CheckPlaceVisitsAsync).
        public HashSet<Guid> NearbyPlaceIds { get; set; } = new();
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