using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Weekly summary push: one combined notification per customer covering the previous Monday to
/// Sunday (Sri Lanka calendar days; fixed UTC+5:30, no daylight saving).
///
/// Cost control, because trip distance is built from raw GPS points plus archived S3 data:
///  - runs only on Monday and (as catch-up) Tuesday, and only for customers not yet handled;
///  - at most <see cref="MaxCustomersPerRun"/> customers per run, one at a time, with a short pause,
///    so it never competes with live traffic (the worker repeats hourly until everyone is done);
///  - dry run computes for a tiny sample only.
///
/// Ordering per customer: compute, then claim the week (atomic UPDATE), then push. A failure while
/// computing leaves the week unclaimed so the next run retries; the claim makes a duplicate send
/// impossible even with two instances.
/// </summary>
public class WeeklySummaryNotifier : IWeeklySummaryNotifier
{
    private static readonly TimeSpan SriLankaOffset = TimeSpan.FromMinutes(330);
    private static readonly TimeSpan PauseBetweenCustomers = TimeSpan.FromMilliseconds(200);

    public const int MaxCustomersPerRun = 300;
    public const int DryRunSample = 3;

    private sealed record VehicleRow(Guid CustomerId, Guid VehicleId, string VehicleNumber);

    private readonly ShaloTrackDbContext _db;
    private readonly IGpsTrackingService _gps;
    private readonly IPushNotificationService _push;
    private readonly ILogger<WeeklySummaryNotifier> _logger;

    public WeeklySummaryNotifier(
        ShaloTrackDbContext db,
        IGpsTrackingService gps,
        IPushNotificationService push,
        ILogger<WeeklySummaryNotifier> logger)
    {
        _db = db;
        _gps = gps;
        _push = push;
        _logger = logger;
    }

    /// <summary>Monday of the week to summarise: the previous full Monday-Sunday before today (local).</summary>
    public static DateOnly WeekToCover(DateTime nowUtc)
    {
        var today = DateOnly.FromDateTime(nowUtc + SriLankaOffset);
        var sinceMonday = ((int)today.DayOfWeek + 6) % 7; // Monday = 0 ... Sunday = 6
        return today.AddDays(-sinceMonday - 7);
    }

    /// <summary>Monday sends; Tuesday only catches up anyone missed (downtime, a failed run).</summary>
    public static bool IsSendDay(DateTime nowUtc)
    {
        var day = DateOnly.FromDateTime(nowUtc + SriLankaOffset).DayOfWeek;
        return day == DayOfWeek.Monday || day == DayOfWeek.Tuesday;
    }

    public async Task<WeeklySummaryRunResult> RunAsync(DateTime nowUtc, bool dryRun, CancellationToken cancellationToken = default)
    {
        if (!dryRun && !IsSendDay(nowUtc))
            return new WeeklySummaryRunResult(0, 0, 0);

        var weekStart = WeekToCover(nowUtc);
        var fromUtc = DateTime.SpecifyKind(weekStart.ToDateTime(TimeOnly.MinValue) - SriLankaOffset, DateTimeKind.Utc);
        var toUtc = fromUtc.AddDays(7);

        var batch = dryRun ? DryRunSample : MaxCustomersPerRun;

        // Customers still owed this week's summary: switched on, active, a real vehicle, and at
        // least one registered device to push to (no token = nobody to notify, so nothing to compute).
        var customerIds = await _db.Customers
            .AsNoTracking()
            .Where(c => c.WeeklySummaryEnabled
                        && c.AccountStatus == CustomerStatus.Active
                        && (c.LastWeeklySummaryFor == null || c.LastWeeklySummaryFor < weekStart)
                        && _db.CustomerFcmTokens.Any(t => t.CustomerId == c.CustomerId)
                        && _db.Vehicles.Any(v => v.CustomerId == c.CustomerId && v.IsActive && !v.IsDemoVehicle))
            .OrderBy(c => c.CustomerId)
            .Select(c => c.CustomerId)
            .Take(batch)
            .ToListAsync(cancellationToken);

        if (customerIds.Count == 0)
        {
            _logger.LogInformation("WeeklySummaryNotifier: nobody left to summarise for the week of {Week} (dryRun={DryRun}).", weekStart, dryRun);
            return new WeeklySummaryRunResult(0, 0, 0);
        }

        var vehiclesByCustomer = (await _db.Vehicles
                .AsNoTracking()
                .Where(v => customerIds.Contains(v.CustomerId) && v.IsActive && !v.IsDemoVehicle)
                .Select(v => new VehicleRow(v.CustomerId, v.VehicleId, v.VehicleNumber))
                .ToListAsync(cancellationToken))
            .GroupBy(v => v.CustomerId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // One pass over the week's overspeed alerts for this batch's vehicles (not one query per customer).
        var allVehicleIds = vehiclesByCustomer.Values.SelectMany(l => l.Select(v => v.VehicleId)).ToList();
        var overspeedByVehicle = await _db.Alerts
            .AsNoTracking()
            .Where(a => a.AlertType == AlertType.Overspeed
                        && a.TriggeredAt >= fromUtc && a.TriggeredAt < toUtc
                        && allVehicleIds.Contains(a.VehicleId))
            .GroupBy(a => a.VehicleId)
            .Select(g => new { VehicleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.VehicleId, x => x.Count, cancellationToken);

        int sent = 0, skipped = 0, failed = 0;

        foreach (var customerId in customerIds)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var vehicles = vehiclesByCustomer.GetValueOrDefault(customerId) ?? new List<VehicleRow>();
            decimal km = 0;
            int trips = 0, movedVehicles = 0, overspeed = 0;
            bool computeFailed = false;

            foreach (var v in vehicles)
            {
                try
                {
                    var report = await _gps.GetTripsReportForSystemAsync(v.VehicleId, fromUtc, toUtc);
                    if (report is not null)
                    {
                        var vehicleKm = report.Trips.Sum(t => t.DistanceKm);
                        km += vehicleKm;
                        trips += report.Trips.Count;
                        if (report.Trips.Count > 0) movedVehicles++;
                    }
                    overspeed += overspeedByVehicle.GetValueOrDefault(v.VehicleId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WeeklySummaryNotifier: could not compute vehicle {VehicleId}; customer {CustomerId} will be retried.", v.VehicleId, customerId);
                    computeFailed = true;
                    break;
                }
            }

            if (computeFailed)
            {
                failed++;
                continue; // week not claimed: the next run retries
            }

            bool nothingToSay = km < 1m && overspeed == 0;
            var (title, body) = BuildMessage(vehicles.Count == 1 ? vehicles[0].VehicleNumber : null, km, trips, movedVehicles, overspeed);

            if (dryRun)
            {
                _logger.LogWarning(
                    "WeeklySummaryNotifier: DRY RUN -- customer {CustomerId}: {Km:F1} km, {Trips} trips, {Overspeed} overspeed alerts, " +
                    "{Action}. Nothing was sent or recorded. Set WeeklySummary:DryRun=false to enable.",
                    customerId, km, trips, overspeed, nothingToSay ? "nothing to send" : "would send: " + body);
                skipped++;
                continue;
            }

            // Claim the week atomically. Zero rows = someone else already handled it (or the
            // customer switched it off in the meantime): do not send.
            var claimed = await _db.Customers
                .Where(c => c.CustomerId == customerId
                            && c.WeeklySummaryEnabled
                            && (c.LastWeeklySummaryFor == null || c.LastWeeklySummaryFor < weekStart))
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastWeeklySummaryFor, (DateOnly?)weekStart), cancellationToken);

            if (claimed == 0 || nothingToSay)
            {
                skipped++;
            }
            else
            {
                try
                {
                    await _push.SendAlertPushAsync(customerId, title, body, new Dictionary<string, string>
                    {
                        ["type"] = "weekly_summary",
                        ["weekStart"] = weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    });
                    sent++;
                }
                catch (Exception ex)
                {
                    // Already claimed, so it will not retry; one failed push must not stop the others.
                    _logger.LogError(ex, "Weekly summary push failed for customer {CustomerId}.", customerId);
                    failed++;
                }
            }

            try
            {
                await Task.Delay(PauseBetweenCustomers, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation(
            "WeeklySummaryNotifier: week of {Week}: {Sent} sent, {Skipped} skipped, {Failed} failed (dryRun={Dry}).",
            weekStart, sent, skipped, failed, dryRun);

        return new WeeklySummaryRunResult(sent, skipped, failed);
    }

    private static (string Title, string Body) BuildMessage(
        string? singleVehicleNumber, decimal km, int trips, int movedVehicles, int overspeed)
    {
        var distance = Math.Round(km).ToString("N0", CultureInfo.InvariantCulture);
        var tripText = trips == 1 ? "1 trip" : $"{trips} trips";

        var lead = singleVehicleNumber is not null
            ? $"{singleVehicleNumber}: {distance} km in {tripText}."
            : $"{distance} km in {tripText} across {movedVehicles} {(movedVehicles == 1 ? "vehicle" : "vehicles")}.";

        var tail = overspeed == 0
            ? " No overspeed alerts."
            : $" {overspeed} overspeed {(overspeed == 1 ? "alert" : "alerts")}.";

        return ("Your week with ShaloTrack", lead + tail);
    }
}