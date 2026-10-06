using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Sends the licence / insurance / service reminders as push notifications (FCM tokens already
/// live in this database). Dates are Sri Lanka calendar dates; Sri Lanka has no daylight saving,
/// so a fixed UTC+5:30 offset is exact and avoids depending on tzdata in the container.
///
/// Same safety pattern as SubscriptionReminderService: only the CURRENT threshold is sent (if the
/// worker was down and 5 days are left, the "7 days" notice goes, not 30/14/7), and the ledger row
/// is written BEFORE the push so a duplicate is impossible.
/// </summary>
public class VehicleReminderNotifier : IVehicleReminderNotifier
{
    private static readonly TimeSpan SriLankaOffset = TimeSpan.FromMinutes(330);

    // Smallest first. 0 = due today.
    public static readonly int[] Thresholds = { 0, 1, 7, 14, 30 };

    // -1 = "recently overdue": one notice, only within this many days after the date, so switching
    // the feature on later never nags about something that lapsed months ago.
    public const int OverdueNoticeDays = 3;
    public const int OverdueThreshold = -1;

    private readonly ShaloTrackDbContext _db;
    private readonly IPushNotificationService _push;
    private readonly ILogger<VehicleReminderNotifier> _logger;

    public VehicleReminderNotifier(
        ShaloTrackDbContext db,
        IPushNotificationService push,
        ILogger<VehicleReminderNotifier> logger)
    {
        _db = db;
        _push = push;
        _logger = logger;
    }

    public static DateOnly LocalToday(DateTime nowUtc) => DateOnly.FromDateTime(nowUtc + SriLankaOffset);

    /// <summary>The notice threshold that applies with this many days left, or null if none.</summary>
    public static int? PickThreshold(int daysLeft)
    {
        if (daysLeft < 0)
            return daysLeft >= -OverdueNoticeDays ? OverdueThreshold : null;

        foreach (var t in Thresholds)
        {
            if (daysLeft <= t) return t;
        }

        return null; // more than 30 days away
    }

    public async Task<VehicleReminderRunResult> RunAsync(DateTime nowUtc, bool dryRun, CancellationToken cancellationToken = default)
    {
        var today = LocalToday(nowUtc);
        var windowStart = today.AddDays(-OverdueNoticeDays);
        var windowEnd = today.AddDays(Thresholds[^1]);

        var rows = await (
                from r in _db.VehicleReminders
                join v in _db.Vehicles on r.VehicleId equals v.VehicleId
                where r.DueDate >= windowStart && r.DueDate <= windowEnd && v.IsActive && !v.IsDemoVehicle
                select new { r.ReminderId, r.Type, r.DueDate, v.VehicleNumber, v.CustomerId })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            _logger.LogInformation("VehicleReminderNotifier: nothing within the notice window (dryRun={DryRun}).", dryRun);
            return new VehicleReminderRunResult(0, 0);
        }

        var ids = rows.Select(r => r.ReminderId).ToList();
        var already = (await _db.VehicleReminderNotices
                .Where(n => ids.Contains(n.ReminderId))
                .Select(n => new { n.ReminderId, n.Threshold, n.DueDate })
                .ToListAsync(cancellationToken))
            .Select(n => (n.ReminderId, n.Threshold, n.DueDate))
            .ToHashSet();

        int sent = 0, skipped = 0;

        foreach (var row in rows)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var daysLeft = row.DueDate.DayNumber - today.DayNumber;
            var threshold = PickThreshold(daysLeft);

            if (threshold is null || already.Contains((row.ReminderId, threshold.Value, row.DueDate)))
            {
                skipped++;
                continue;
            }

            var (title, body) = BuildMessage(row.Type, row.VehicleNumber, threshold.Value);

            if (await DeliverAsync(row.ReminderId, threshold.Value, row.DueDate, row.CustomerId, row.Type, title, body, nowUtc, dryRun, cancellationToken))
                sent++;
            else
                skipped++;
        }

        _logger.LogInformation("VehicleReminderNotifier: {Sent} sent, {Skipped} skipped (dryRun={DryRun}).", sent, skipped, dryRun);
        return new VehicleReminderRunResult(sent, skipped);
    }

    private async Task<bool> DeliverAsync(
        Guid reminderId, int threshold, DateOnly dueDate, Guid customerId, VehicleReminderType type,
        string title, string body, DateTime nowUtc, bool dryRun, CancellationToken cancellationToken)
    {
        if (dryRun)
        {
            _logger.LogWarning(
                "VehicleReminderNotifier: DRY RUN -- would send threshold {Threshold} ({Type}) to customer {CustomerId}: {Title}. " +
                "Nothing was sent or recorded. Set VehicleReminders:DryRun=false to enable.",
                threshold, type, customerId, title);
            return true;
        }

        try
        {
            _db.VehicleReminderNotices.Add(new VehicleReminderNotice
            {
                NoticeId = Guid.NewGuid(),
                ReminderId = reminderId,
                Threshold = threshold,
                DueDate = dueDate,
                SentAt = nowUtc,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            // Unique index hit (another worker got there first) or the reminder was deleted meanwhile.
            _logger.LogInformation(ex, "Vehicle reminder {ReminderId} threshold {Threshold} already recorded or removed; skipping.", reminderId, threshold);
            _db.ChangeTracker.Clear();
            return false;
        }

        try
        {
            await _push.SendAlertPushAsync(customerId, title, body, new Dictionary<string, string>
            {
                ["type"] = "vehicle_reminder",
                ["reminderType"] = ((int)type).ToString(),
                ["threshold"] = threshold.ToString(),
            });
        }
        catch (Exception ex)
        {
            // Already recorded, so it will not retry; one failed push must not stop the others.
            _logger.LogError(ex, "Vehicle reminder push failed for customer {CustomerId} (reminder {ReminderId}).", customerId, reminderId);
        }

        return true;
    }

    public static string Label(VehicleReminderType type) => type switch
    {
        VehicleReminderType.RevenueLicence => "revenue licence",
        VehicleReminderType.Insurance => "insurance",
        VehicleReminderType.ServiceDue => "service",
        _ => "reminder",
    };

    // The user's Notes are deliberately NOT included: a push shows on the lock screen.
    private static (string Title, string Body) BuildMessage(VehicleReminderType type, string vehicleNumber, int threshold)
    {
        var what = Label(type);
        var title = type == VehicleReminderType.ServiceDue ? "Service reminder" : $"{char.ToUpper(what[0])}{what[1..]} reminder";

        var when = threshold switch
        {
            0 => "is due today",
            1 => "is due tomorrow",
            OverdueThreshold => "is overdue",
            _ => $"is due in {threshold} days",
        };

        return (title, $"{vehicleNumber}: {what} {when}.");
    }
}