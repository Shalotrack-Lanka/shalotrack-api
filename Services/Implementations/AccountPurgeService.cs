using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Constants;
using ShaloTrack_API.Data;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// The permanent part of "delete my account". Runs for customers whose request is older than 30 days.
///
/// What goes: the Firebase account; the customer's GPS history, alerts and device events for their
/// vehicles during their ownership (the assignment windows); saved places, geofences, emergency
/// contacts, reminders, live links, shares, push tokens; the text of their complaints and their
/// replies; payment slip images, references and notes. Devices are unlinked (the device and its
/// subscription stay with the company; a later owner sees data only from their own bind date).
/// What stays, anonymised: the customer and vehicle rows themselves (plate, chassis and engine
/// numbers, name, phone, NIC, email erased) and renewal/subscription rows (amount, dates, decision),
/// because payment history references them. S3 trip archives age out under the existing retention.
///
/// Every step is idempotent and the customer is marked deleted LAST, so a failure at any point just
/// means the next hourly run picks the same customer up again. Large deletes are chunked.
/// </summary>
public class AccountPurgeService : IAccountPurgeService
{
    private const int ChunkSize = 5000;
    private const int MaxChunksPerCustomerPerRun = 400; // ~2 million rows, then continue next run

    private readonly ShaloTrackDbContext _db;
    private readonly IFirebaseUserAdmin _firebase;
    private readonly ILogger<AccountPurgeService> _logger;

    public AccountPurgeService(ShaloTrackDbContext db, IFirebaseUserAdmin firebase, ILogger<AccountPurgeService> logger)
    {
        _db = db;
        _firebase = firebase;
        _logger = logger;
    }

    private enum Outcome { Done, Incomplete, DryRun }

    private sealed record Window(Guid DeviceId, DateTime From, DateTime To);

    public async Task<AccountPurgeRunResult> RunAsync(DateTime nowUtc, bool dryRun, int batchSize, CancellationToken cancellationToken = default)
    {
        var cutoff = nowUtc - TimeSpan.FromDays(AccountDeletion.GraceDays);

        var due = await _db.Customers.AsNoTracking()
            .Where(c => c.DeletionRequestedAt != null && c.DeletionRequestedAt <= cutoff && c.DeletedAt == null)
            .OrderBy(c => c.DeletionRequestedAt)
            .Take(Math.Max(1, batchSize))
            .Select(c => new { c.CustomerId, c.FirebaseUid })
            .ToListAsync(cancellationToken);

        int purged = 0, incomplete = 0, failed = 0;

        foreach (var c in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var outcome = await PurgeOneAsync(c.CustomerId, c.FirebaseUid, nowUtc, dryRun, cancellationToken);
                if (outcome == Outcome.Done) purged++;
                else if (outcome == Outcome.Incomplete) incomplete++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "AccountPurge: customer {CustomerId} failed -- will retry on the next run.", c.CustomerId);
            }
        }

        return new AccountPurgeRunResult(due.Count, purged, incomplete, failed, dryRun);
    }

    private async Task<Outcome> PurgeOneAsync(Guid customerId, string? uid, DateTime nowUtc, bool dryRun, CancellationToken ct)
    {
        // The shared demo vehicle is never touched.
        var vehicleIds = await _db.Vehicles.AsNoTracking()
            .Where(v => v.CustomerId == customerId && !v.IsDemoVehicle)
            .Select(v => v.VehicleId)
            .ToListAsync(ct);

        if (dryRun)
        {
            var windows = await GetWindowsAsync(vehicleIds, nowUtc, ct);
            long gps = 0;
            foreach (var w in windows)
            {
                gps += await _db.GpsTrackings.AsNoTracking()
                    .CountAsync(x => x.DeviceId == w.DeviceId && x.EventTime >= w.From && x.EventTime <= w.To, ct);
            }

            var alerts = await _db.Alerts.AsNoTracking().CountAsync(a => vehicleIds.Contains(a.VehicleId), ct);
            var events = await _db.DeviceEvents.AsNoTracking().CountAsync(e => e.VehicleId != null && vehicleIds.Contains(e.VehicleId.Value), ct);
            var active = await _db.DeviceAssignments.AsNoTracking()
                .CountAsync(a => vehicleIds.Contains(a.VehicleId) && a.Status == AssignmentStatus.Active, ct);

            _logger.LogInformation(
                "AccountPurge DRY RUN: customer {CustomerId} would erase {Gps} GPS points, {Alerts} alerts, {Events} device events " +
                "across {Vehicles} vehicles, unlink {Active} devices and {Firebase}. Nothing was changed.",
                customerId, gps, alerts, events, vehicleIds.Count, active,
                string.IsNullOrEmpty(uid) ? "has no Firebase account to delete" : "delete the Firebase account");

            return Outcome.DryRun;
        }

        // 1. Firebase first: if this fails nothing else has changed. A retry after a partial purge
        //    finds "user not found", which counts as success.
        if (!string.IsNullOrEmpty(uid))
            await _firebase.DeleteUserAsync(uid, ct);

        // 2. Unlink devices (same effect as Unassign: Removed + RemovedAt). Done BEFORE the GPS delete
        //    so the window is closed and nothing new can land inside it.
        await _db.DeviceAssignments
            .Where(a => vehicleIds.Contains(a.VehicleId) && a.Status == AssignmentStatus.Active)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, AssignmentStatus.Removed)
                .SetProperty(a => a.RemovedAt, (DateTime?)nowUtc), ct);

        // 3. Big deletes, chunked, outside the transaction (each chunk is its own small transaction).
        var chunks = 0;

        foreach (var w in await GetWindowsAsync(vehicleIds, nowUtc, ct))
        {
            while (true)
            {
                var ids = await _db.GpsTrackings
                    .Where(x => x.DeviceId == w.DeviceId && x.EventTime >= w.From && x.EventTime <= w.To)
                    .Select(x => x.TrackingId)
                    .Take(ChunkSize)
                    .ToListAsync(ct);
                if (ids.Count == 0) break;

                await _db.GpsTrackings.Where(x => ids.Contains(x.TrackingId)).ExecuteDeleteAsync(ct);

                if (++chunks >= MaxChunksPerCustomerPerRun) return Incomplete(customerId);
                await Task.Delay(100, ct);
            }
        }

        while (true)
        {
            var ids = await _db.DeviceEvents
                .Where(e => e.VehicleId != null && vehicleIds.Contains(e.VehicleId.Value))
                .Select(e => e.EventId)
                .Take(ChunkSize)
                .ToListAsync(ct);
            if (ids.Count == 0) break;

            await _db.DeviceEvents.Where(e => ids.Contains(e.EventId)).ExecuteDeleteAsync(ct);

            if (++chunks >= MaxChunksPerCustomerPerRun) return Incomplete(customerId);
            await Task.Delay(100, ct);
        }

        while (true)
        {
            var ids = await _db.Alerts
                .Where(a => vehicleIds.Contains(a.VehicleId))
                .Select(a => a.AlertId)
                .Take(ChunkSize)
                .ToListAsync(ct);
            if (ids.Count == 0) break;

            await _db.Alerts.Where(a => ids.Contains(a.AlertId)).ExecuteDeleteAsync(ct);

            if (++chunks >= MaxChunksPerCustomerPerRun) return Incomplete(customerId);
            await Task.Delay(100, ct);
        }

        // 4. Everything else, in one transaction. The customer row is anonymised and marked deleted
        //    last, so a failure anywhere rolls back and the next run starts this part again.
        var complaintIds = await _db.Complaints.Where(c => c.CustomerId == customerId).Select(c => c.ComplaintId).ToListAsync(ct);
        var renewalIds = await _db.RenewalRequests.Where(r => r.CustomerId == customerId).Select(r => r.RenewalRequestId).ToListAsync(ct);
        var reminderIds = await _db.VehicleReminders.Where(r => vehicleIds.Contains(r.VehicleId)).Select(r => r.ReminderId).ToListAsync(ct);

        await using (var tx = await _db.Database.BeginTransactionAsync(ct))
        {
            await _db.CurrentLocations.Where(l => vehicleIds.Contains(l.VehicleId)).ExecuteDeleteAsync(ct);

            await _db.VehicleReminderNotices.Where(n => reminderIds.Contains(n.ReminderId)).ExecuteDeleteAsync(ct);
            await _db.VehicleReminders.Where(r => vehicleIds.Contains(r.VehicleId)).ExecuteDeleteAsync(ct);

            await _db.LiveShareLinks.Where(l => l.CustomerId == customerId).ExecuteDeleteAsync(ct);
            await _db.Geofences.Where(g => g.CustomerId == customerId).ExecuteDeleteAsync(ct);
            await _db.SavedPlaces.Where(p => p.CustomerId == customerId).ExecuteDeleteAsync(ct);
            await _db.EmergencyContacts.Where(e => e.CustomerId == customerId).ExecuteDeleteAsync(ct);
            await _db.CustomerFcmTokens.Where(t => t.CustomerId == customerId).ExecuteDeleteAsync(ct);
            await _db.VehicleShares
                .Where(s => s.OwnerCustomerId == customerId || s.SharedWithCustomerId == customerId)
                .ExecuteDeleteAsync(ct);

            // Complaints: the words are erased, the case (category, status, dates, dealer) stays.
            // Staff replies stay: they are the company's own record of its answer.
            await _db.ComplaintReplies
                .Where(r => complaintIds.Contains(r.ComplaintId) && r.AuthorType == ComplaintReplyAuthorType.Customer)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Message, AccountDeletion.RemovedText)
                    .SetProperty(r => r.AuthorName, AccountDeletion.DeletedName), ct);
            await _db.Complaints
                .Where(c => c.CustomerId == customerId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Description, AccountDeletion.RemovedText), ct);

            // Renewals: slip image, reference and note erased; amount, dates and decision stay.
            // An open request is cancelled (its slip is gone, nobody should approve it).
            await _db.RenewalSlips.Where(s => renewalIds.Contains(s.RenewalRequestId)).ExecuteDeleteAsync(ct);
            await _db.RenewalRequests
                .Where(r => r.CustomerId == customerId
                            && (r.Status == RenewalStatus.AwaitingSlip || r.Status == RenewalStatus.PendingReview))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, RenewalStatus.Cancelled)
                    .SetProperty(r => r.DecisionReason, "Account deleted")
                    .SetProperty(r => r.DecidedAt, (DateTime?)nowUtc), ct);
            await _db.RenewalRequests
                .Where(r => r.CustomerId == customerId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.PaymentReference, (string?)null)
                    .SetProperty(r => r.CustomerNote, (string?)null)
                    .SetProperty(r => r.SlipUploadedAt, (DateTime?)null)
                    .SetProperty(r => r.SlipSha256, (string?)null), ct);

            // Vehicles: anonymised in place (plate, chassis, engine, colour gone).
            foreach (var vehicleId in vehicleIds)
            {
                var plate = "DELETED-" + vehicleId.ToString("N")[..8].ToUpperInvariant();
                await _db.Vehicles
                    .Where(v => v.VehicleId == vehicleId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(v => v.VehicleNumber, plate)
                        .SetProperty(v => v.ChassisNumber, (string?)null)
                        .SetProperty(v => v.EngineNumber, (string?)null)
                        .SetProperty(v => v.Color, (string?)null)
                        .SetProperty(v => v.SpeedLimitKmh, (int?)null)
                        .SetProperty(v => v.IdleAlertMinutes, (int?)null)
                        .SetProperty(v => v.IsActive, false)
                        .SetProperty(v => v.UpdatedAt, nowUtc), ct);
            }

            // Customer: anonymised and marked deleted. FirebaseUid cleared so the unique index and any
            // future registration are unaffected.
            var tag = customerId.ToString("N")[..12];
            var deletedEmail = $"deleted-{tag}@{AccountDeletion.DeletedEmailDomain}";
            var deletedRef = $"deleted-{tag}";
            await _db.Customers
                .Where(c => c.CustomerId == customerId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.FullName, AccountDeletion.DeletedName)
                    .SetProperty(c => c.Email, deletedEmail)
                    .SetProperty(c => c.PhoneNumber, deletedRef)
                    .SetProperty(c => c.NicNumber, deletedRef)
                    .SetProperty(c => c.Address, (string?)null)
                    .SetProperty(c => c.ProfileImage, (string?)null)
                    .SetProperty(c => c.FirebaseUid, (string?)null)
                    .SetProperty(c => c.AccountStatus, CustomerStatus.Inactive)
                    .SetProperty(c => c.WeeklySummaryEnabled, false)
                    .SetProperty(c => c.DeletedAt, (DateTime?)nowUtc)
                    .SetProperty(c => c.UpdatedAt, nowUtc), ct);

            await tx.CommitAsync(ct);
        }

        _logger.LogInformation("AccountPurge: customer {CustomerId} permanently erased ({Vehicles} vehicles anonymised).", customerId, vehicleIds.Count);
        return Outcome.Done;
    }

    private Outcome Incomplete(Guid customerId)
    {
        _logger.LogInformation("AccountPurge: customer {CustomerId} has more history to erase; continuing on the next run.", customerId);
        return Outcome.Incomplete;
    }

    /// <summary>One window per assignment of the customer's vehicles: [bound, unbound or now].</summary>
    private async Task<List<Window>> GetWindowsAsync(List<Guid> vehicleIds, DateTime nowUtc, CancellationToken ct)
    {
        var rows = await _db.DeviceAssignments.AsNoTracking()
            .Where(a => vehicleIds.Contains(a.VehicleId))
            .Select(a => new { a.DeviceId, a.AssignedAt, a.RemovedAt })
            .ToListAsync(ct);

        return rows.Select(r => new Window(r.DeviceId, r.AssignedAt, r.RemovedAt ?? nowUtc)).ToList();
    }
}