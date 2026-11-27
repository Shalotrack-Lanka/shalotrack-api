using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Hubs;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Once a minute, re-checks that every customer connection in a live-location group (LiveGroupRegistry) still
/// has access to that vehicle, using the same rule as LocationHub.JoinVehicleGroup: owner, ACCEPTED share, or
/// the demo vehicle. A connection that lost access (share revoked, declined or left, vehicle moved to another
/// owner, account erased) is removed from the group and told "AccessEnded".
///
/// Closes a real leak: group membership used to be checked only at join, so a revoked viewer kept receiving the
/// vehicle's live position for as long as their connection stayed open.
///
/// Cost: nothing when no customer is connected; otherwise two indexed queries per minute per instance, however
/// many connections there are (vehicle ids are batched). Read-only: it never writes to the database.
/// </summary>
public class LiveAccessSweeper : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);
    private const int BatchSize = 500;

    private readonly LiveGroupRegistry _registry;
    private readonly IHubContext<LocationHub> _hub;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LiveAccessSweeper> _logger;

    public LiveAccessSweeper(LiveGroupRegistry registry, IHubContext<LocationHub> hub,
        IServiceScopeFactory scopeFactory, ILogger<LiveAccessSweeper> logger)
    {
        _registry = registry;
        _hub = hub;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed sweep keeps everyone where they are until the next minute; it never grants access.
                _logger.LogError(ex, "LiveAccessSweeper: sweep failed, retrying next interval.");
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        if (_registry.IsEmpty) return;
        var members = _registry.Snapshot();
        if (members.Count == 0) return;

        var vehicleIds = members.Select(m => m.VehicleId).Distinct().ToList();
        var allowed = new HashSet<(Guid VehicleId, string Uid)>();
        var demo = new HashSet<Guid>();

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShaloTrackDbContext>();
            foreach (var batch in vehicleIds.Chunk(BatchSize))
            {
                var owners = await db.Vehicles
                    .Where(v => batch.Contains(v.VehicleId))
                    .Select(v => new { v.VehicleId, v.IsDemoVehicle, Uid = v.Customer.FirebaseUid })
                    .ToListAsync(ct);
                foreach (var o in owners)
                {
                    if (o.IsDemoVehicle) demo.Add(o.VehicleId);
                    if (!string.IsNullOrEmpty(o.Uid)) allowed.Add((o.VehicleId, o.Uid));
                }

                var viewers = await db.VehicleShares
                    .Where(s => batch.Contains(s.VehicleId)
                        && s.Status == VehicleShareStatus.Accepted
                        && s.SharedWithCustomerId != null)
                    .Select(s => new { s.VehicleId, Uid = s.SharedWithCustomer!.FirebaseUid })
                    .ToListAsync(ct);
                foreach (var v in viewers)
                {
                    if (!string.IsNullOrEmpty(v.Uid)) allowed.Add((v.VehicleId, v.Uid));
                }
            }
        }

        int removed = 0;
        foreach (var m in members)
        {
            if (demo.Contains(m.VehicleId)) continue;
            if (allowed.Contains((m.VehicleId, m.FirebaseUid))) continue;

            var group = LocationHub.GroupName(m.VehicleId);
            await _hub.Groups.RemoveFromGroupAsync(m.ConnectionId, group, ct);
            _registry.Remove(m.ConnectionId, m.VehicleId);
            await _hub.Clients.Client(m.ConnectionId).SendAsync(LocationHub.AccessEndedEvent, group, ct);
            removed++;
        }

        if (removed > 0)
        {
            // Counts only: no vehicle ids or user ids in the log.
            _logger.LogInformation("LiveAccessSweeper: removed {Count} live connection(s) that no longer have access.", removed);
        }
    }
}
