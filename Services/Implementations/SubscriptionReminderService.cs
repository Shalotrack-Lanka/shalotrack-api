using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Renewal reminders. Source of truth is DeviceSubscriptionStatuses (fed every minute by the
/// admin portal's subscriptions:sync-to-api). The customer to notify is the owner of the
/// vehicle the device is actively assigned to; the push goes through the existing
/// IPushNotificationService (FCM tokens already live in this database).
///
/// How the admin portal shapes the data this relies on: when a subscription lapses the admin
/// sets IsActive=false and clears ExpiresAt. So the "subscription ended" notice cannot be
/// found from ExpiresAt; it is derived from the final (1 day) reminder in the ledger instead.
/// </summary>
public class SubscriptionReminderService : ISubscriptionReminderService
{
    // Days before expiry, smallest first. A device gets only the CURRENT milestone: if the
    // worker was down and 6 days are left, it sends the "7 days" reminder, not 14 and 7.
    public static readonly int[] Milestones = { 1, 7, 14 };

    // Only send the "ended" notice shortly after the lapse, so switching this on later never
    // spams customers whose subscriptions ended weeks ago.
    private const int EndedNoticeWindowDays = 3;

    private readonly ShaloTrackDbContext _db;
    private readonly IPushNotificationService _push;
    private readonly ILogger<SubscriptionReminderService> _logger;

    public SubscriptionReminderService(
        ShaloTrackDbContext db,
        IPushNotificationService push,
        ILogger<SubscriptionReminderService> logger)
    {
        _db = db;
        _push = push;
        _logger = logger;
    }

    /// <summary>The milestone that applies with this much time left, or null if none (expired, or more than 14 days).</summary>
    public static int? PickMilestone(TimeSpan timeLeft)
    {
        if (timeLeft <= TimeSpan.Zero) return null;

        foreach (var milestone in Milestones)
        {
            if (timeLeft <= TimeSpan.FromDays(milestone)) return milestone;
        }

        return null;
    }

    public async Task<SubscriptionReminderRunResult> RunAsync(DateTime nowUtc, bool dryRun, CancellationToken cancellationToken = default)
    {
        int upcomingSent = 0, endedSent = 0, skipped = 0;

        // ---------- 1. Upcoming expiries (14 / 7 / 1 days) ----------
        var horizon = nowUtc.AddDays(Milestones[^1]);
        var expiring = await _db.DeviceSubscriptionStatuses
            .Where(s => s.IsActive && s.ExpiresAt != null && s.ExpiresAt > nowUtc && s.ExpiresAt <= horizon)
            .ToListAsync(cancellationToken);

        var candidates = new List<(string Imei, DateTime ExpiresAt, int Milestone)>();
        foreach (var status in expiring)
        {
            var expiresAt = status.ExpiresAt!.Value;
            var milestone = PickMilestone(expiresAt - nowUtc);
            if (milestone is not null) candidates.Add((status.ImeiNumber, expiresAt, milestone.Value));
        }

        // ---------- 2. "Subscription ended" notices ----------
        var since = nowUtc.AddDays(-EndedNoticeWindowDays);
        var finalWarnings = await _db.SubscriptionReminderLogs
            .Where(l => l.Milestone == 1 && l.ExpiresAt <= nowUtc && l.ExpiresAt > since)
            .ToListAsync(cancellationToken);

        var allImeis = candidates.Select(c => c.Imei)
            .Concat(finalWarnings.Select(l => l.ImeiNumber))
            .Distinct()
            .ToList();

        if (allImeis.Count == 0)
        {
            // Say so: a silent run is indistinguishable from a worker that never started.
            _logger.LogInformation(
                "SubscriptionReminderService: no devices within the reminder window (dryRun={DryRun}).", dryRun);
            return new SubscriptionReminderRunResult(0, 0, 0);
        }

        var alreadyLogged = (await _db.SubscriptionReminderLogs
                .Where(l => allImeis.Contains(l.ImeiNumber))
                .ToListAsync(cancellationToken))
            .Select(l => (l.ImeiNumber, l.Milestone, l.ExpiresAt.Ticks))
            .ToHashSet();

        var currentStatus = await _db.DeviceSubscriptionStatuses
            .Where(s => allImeis.Contains(s.ImeiNumber))
            .ToDictionaryAsync(s => s.ImeiNumber, cancellationToken);

        // IMEI -> (vehicle number, owner) for devices actively assigned to a real customer vehicle.
        var targetRows = await (
                from a in _db.DeviceAssignments
                join d in _db.GpsDevices on a.DeviceId equals d.DeviceId
                join v in _db.Vehicles on a.VehicleId equals v.VehicleId
                where a.Status == AssignmentStatus.Active
                      && allImeis.Contains(d.ImeiNumber)
                      && v.IsActive
                      && !v.IsDemoVehicle
                select new { d.ImeiNumber, v.VehicleNumber, v.CustomerId })
            .ToListAsync(cancellationToken);

        var targets = targetRows
            .GroupBy(t => t.ImeiNumber)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var (imei, expiresAt, milestone) in candidates)
        {
            if (cancellationToken.IsCancellationRequested) break;

            if (!targets.TryGetValue(imei, out var target) ||
                alreadyLogged.Contains((imei, milestone, expiresAt.Ticks)))
            {
                skipped++;
                continue;
            }

            var (title, body) = BuildUpcomingMessage(target.VehicleNumber, milestone);

            if (await DeliverAsync(imei, milestone, expiresAt, target.CustomerId, title, body, nowUtc, dryRun, cancellationToken))
                upcomingSent++;
            else
                skipped++;
        }

        foreach (var warning in finalWarnings)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var imei = warning.ImeiNumber;

            // Already told, no vehicle to tell, or it was renewed (still active) -> nothing to say.
            // If the subscription is past its end but the admin has not flipped it yet (it does so
            // within a minute), wait for the next run.
            if (alreadyLogged.Contains((imei, 0, warning.ExpiresAt.Ticks)) ||
                !targets.TryGetValue(imei, out var target) ||
                !currentStatus.TryGetValue(imei, out var status) ||
                status.IsActive)
            {
                skipped++;
                continue;
            }

            var (title, body) = BuildEndedMessage(target.VehicleNumber);

            if (await DeliverAsync(imei, 0, warning.ExpiresAt, target.CustomerId, title, body, nowUtc, dryRun, cancellationToken))
                endedSent++;
            else
                skipped++;
        }

        _logger.LogInformation(
            "SubscriptionReminderService: {Upcoming} upcoming, {Ended} ended notice(s), {Skipped} skipped (dryRun={DryRun}).",
            upcomingSent, endedSent, skipped, dryRun);

        return new SubscriptionReminderRunResult(upcomingSent, endedSent, skipped);
    }

    /// <summary>Records the reminder, then pushes it. Returns false when it was not sent.</summary>
    private async Task<bool> DeliverAsync(
        string imei, int milestone, DateTime expiresAt, Guid customerId,
        string title, string body, DateTime nowUtc, bool dryRun, CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            _logger.LogWarning(
                "SubscriptionReminderService: DRY RUN -- would send milestone {Milestone} to customer {CustomerId} " +
                "(device ...{ImeiTail}, expires {ExpiresAt:O}): {Title}. Nothing was sent or recorded. " +
                "Set SubscriptionReminders:DryRun=false to enable.",
                milestone, customerId, imei.Length > 4 ? imei[^4..] : imei, expiresAt, title);
            return true;
        }

        // Record first: the unique index makes this the guard against a duplicate send.
        try
        {
            _db.SubscriptionReminderLogs.Add(new SubscriptionReminderLog
            {
                ReminderId = Guid.NewGuid(),
                ImeiNumber = imei,
                Milestone = milestone,
                ExpiresAt = expiresAt,
                SentAt = nowUtc,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogInformation(ex, "Reminder {Milestone} for device ...{ImeiTail} was already recorded; skipping.",
                milestone, imei.Length > 4 ? imei[^4..] : imei);
            _db.ChangeTracker.Clear();
            return false;
        }

        try
        {
            await _push.SendAlertPushAsync(customerId, title, body, new Dictionary<string, string>
            {
                ["type"] = "subscription_reminder",
                ["milestone"] = milestone.ToString(),
            });
        }
        catch (Exception ex)
        {
            // Already recorded, so it will not retry; one failed push must not stop the others.
            _logger.LogError(ex, "Renewal push failed for customer {CustomerId} (milestone {Milestone}).", customerId, milestone);
        }

        return true;
    }

    private static (string Title, string Body) BuildUpcomingMessage(string vehicleNumber, int milestone)
    {
        var when = milestone == 1 ? "within 24 hours" : $"in {milestone} days";

        return (
            "Subscription ending soon",
            $"Your ShaloTrack subscription for {vehicleNumber} ends {when}. " +
            "Contact your dealer to renew and keep live tracking and trip history.");
    }

    private static (string Title, string Body) BuildEndedMessage(string vehicleNumber) => (
        "Subscription ended",
        $"Your ShaloTrack subscription for {vehicleNumber} has ended. Live tracking and trip history are paused " +
        "(your alerts keep working). Contact your dealer to renew.");
}