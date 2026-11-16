using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Raises the DeviceOffline alert (AlertType 5, already shown by the Android Alerts screen) when a
/// tracker that is assigned to an active vehicle has gone quiet.
///
/// "Quiet" is DeviceStatuses.LastSeen, which the gateway refreshes on every login, heartbeat and status
/// packet. It is measured here, not in the gateway, because a device that vanishes without closing its
/// TCP connection (unplugged, no signal, SIM out of credit) produces no event anywhere.
///
/// Once per outage and no new table: an outage is identified by LastSeen. A device is a candidate only
/// when no DeviceOffline alert exists with TriggeredAt >= its LastSeen. When the device is seen again
/// LastSeen moves past that alert, so the next outage alerts again. Uses the existing index
/// IX_Alerts_DeviceId_AlertType_TriggeredAt.
///
/// Safety valves: the alert is saved BEFORE the push (a failed push is never retried, no duplicate);
/// devices silent for longer than MaxAge are ignored (switching the feature on never floods owners with
/// months-old outages); at most MaxPerSweep alerts per sweep; and when more than half of the recently
/// active devices look offline at once the sweep is skipped, because that pattern means the gateway or
/// the database was down and every device would otherwise get a false alert.
///
/// Assumes a single API instance (same as the other background workers): two instances sweeping at the
/// same moment could each create the alert for the same device.
/// </summary>
public class DeviceOfflineNotifier : IDeviceOfflineNotifier
{
    // Sri Lanka has no daylight saving, so a fixed offset is exact (same approach as VehicleReminderNotifier).
    private static readonly TimeSpan SriLankaOffset = TimeSpan.FromMinutes(330);

    private readonly ShaloTrackDbContext _db;
    private readonly IPushNotificationService _push;
    private readonly ILogger<DeviceOfflineNotifier> _logger;

    public DeviceOfflineNotifier(
        ShaloTrackDbContext db,
        IPushNotificationService push,
        ILogger<DeviceOfflineNotifier> logger)
    {
        _db = db;
        _push = push;
        _logger = logger;
    }

    private sealed record Candidate(Guid DeviceId, Guid VehicleId, string VehicleNumber, Guid CustomerId, DateTime LastSeen);

    public async Task<DeviceOfflineRunResult> RunAsync(
        DateTime nowUtc, DeviceOfflineOptions options, bool dryRun, CancellationToken cancellationToken = default)
    {
        var staleBefore = nowUtc - options.Threshold;
        var notOlderThan = nowUtc - options.MaxAge;

        // Devices on an active, non-demo vehicle that were seen at some point within MaxAge.
        // Anonymous type on purpose: Where/Select composed on top of it always translate to SQL.
        var monitored =
            from s in _db.DeviceStatuses
            join a in _db.DeviceAssignments on s.DeviceId equals a.DeviceId
            join v in _db.Vehicles on a.VehicleId equals v.VehicleId
            where a.Status == AssignmentStatus.Active
                  && v.IsActive
                  && !v.IsDemoVehicle
                  && s.LastSeen != null
                  && s.LastSeen >= notOlderThan
            select new
            {
                s.DeviceId,
                v.VehicleId,
                v.VehicleNumber,
                v.CustomerId,
                LastSeen = s.LastSeen!.Value,
            };

        var rows = await monitored
            .Where(c => c.LastSeen < staleBefore)
            .Where(c => !_db.Alerts.Any(al =>
                al.DeviceId == c.DeviceId
                && al.AlertType == AlertType.DeviceOffline
                && al.TriggeredAt >= c.LastSeen))
            .OrderByDescending(c => c.LastSeen)
            .ToListAsync(cancellationToken);

        var candidates = rows
            .Select(r => new Candidate(r.DeviceId, r.VehicleId, r.VehicleNumber, r.CustomerId, r.LastSeen))
            .DistinctBy(c => c.DeviceId)
            .ToList();

        if (candidates.Count == 0)
            return new DeviceOfflineRunResult(0, 0, false);

        if (candidates.Count >= options.MassOutageMin)
        {
            var monitoredCount = await monitored.Select(c => c.DeviceId).Distinct().CountAsync(cancellationToken);
            if (candidates.Count * 2 > monitoredCount)
            {
                _logger.LogError(
                    "DeviceOfflineNotifier: {Candidates} of {Monitored} recently active devices look offline at once -- " +
                    "that is a gateway or database outage, not {Candidates} broken trackers. Sweep skipped, no alerts created.",
                    candidates.Count, monitoredCount, candidates.Count);
                return new DeviceOfflineRunResult(0, candidates.Count, true);
            }
        }

        int alerted = 0, skipped = 0;

        foreach (var candidate in candidates)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (alerted >= options.MaxPerSweep)
            {
                skipped++;   // picked up by the next sweep
                continue;
            }

            if (await DeliverAsync(candidate, nowUtc, dryRun, logDryRun: alerted < 10, cancellationToken))
                alerted++;
            else
                skipped++;
        }

        _logger.LogInformation(
            "DeviceOfflineNotifier: {Alerted} alerted, {Skipped} skipped (dryRun={DryRun}).", alerted, skipped, dryRun);
        return new DeviceOfflineRunResult(alerted, skipped, false);
    }

    private async Task<bool> DeliverAsync(Candidate c, DateTime nowUtc, bool dryRun, bool logDryRun, CancellationToken cancellationToken)
    {
        var minutes = (int)(nowUtc - c.LastSeen).TotalMinutes;
        var lastSeenLocal = (c.LastSeen + SriLankaOffset).ToString("HH:mm");
        var message = $"No data received for {minutes} min. Last seen at {lastSeenLocal}.";
        var title = $"{c.VehicleNumber}: Device offline";

        if (dryRun)
        {
            // No ledger in a dry run, so the same devices repeat every sweep: only the first few are logged.
            if (logDryRun)
            {
                _logger.LogWarning(
                    "DeviceOfflineNotifier: DRY RUN -- would alert customer {CustomerId} for vehicle {VehicleId} (device {DeviceId}): {Message} " +
                    "Nothing was saved or sent. Set DeviceOffline:DryRun=false to enable.",
                    c.CustomerId, c.VehicleId, c.DeviceId, message);
            }
            return true;
        }

        try
        {
            _db.Alerts.Add(new Alert
            {
                VehicleId = c.VehicleId,
                DeviceId = c.DeviceId,
                AlertType = AlertType.DeviceOffline,
                Message = message,
                TriggeredAt = nowUtc,
                IsRead = false,
                CreatedAt = nowUtc,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "DeviceOfflineNotifier: could not save the alert for device {DeviceId}.", c.DeviceId);
            _db.ChangeTracker.Clear();
            return false;
        }

        // The alert exists now; a push failure must not stop the other devices.
        var recipients = new List<Guid> { c.CustomerId };
        try
        {
            var viewers = await _db.VehicleShares
                .AsNoTracking()
                .Where(s => s.VehicleId == c.VehicleId && s.Status == VehicleShareStatus.Accepted)
                .Select(s => s.SharedWithCustomerId)
                .ToListAsync(cancellationToken);
            recipients.AddRange(viewers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DeviceOfflineNotifier: could not load the viewers of vehicle {VehicleId}; notifying the owner only.", c.VehicleId);
        }

        foreach (var recipient in recipients.Distinct())
        {
            try
            {
                await _push.SendAlertPushAsync(recipient, title, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DeviceOfflineNotifier: push failed for customer {CustomerId}, vehicle {VehicleId} -- the alert was saved.", recipient, c.VehicleId);
            }
        }

        return true;
    }
}
