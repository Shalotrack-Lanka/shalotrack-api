using System.Net;
using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Auth;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.Account;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Implementations;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// The signed-in customer's own data rights: download (this file) and, later in this feature,
/// delete. Every query is scoped to the customer resolved from the token; there is no id
/// parameter anywhere, so one customer can never export another's data.
/// </summary>
public class AccountDataService : IAccountDataService
{
    private const int MaxAlerts = 1000;
    private static readonly TimeSpan AlertWindow = TimeSpan.FromDays(90);

    private readonly ShaloTrackDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AccountDataService(ShaloTrackDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<AccountExportDto>> ExportAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
            return Unauthorized();

        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.FirebaseUid == uid);
        if (customer is null)
            return Unauthorized();

        var id = customer.CustomerId;
        var now = DateTime.UtcNow;

        // ── Vehicles (active ones) + the device currently bound ───────────────────────
        var vehicleRows = await _db.Vehicles.AsNoTracking()
            .Where(v => v.CustomerId == id && v.IsActive)
            .OrderBy(v => v.VehicleNumber)
            .Select(v => new
            {
                v.VehicleId,
                v.VehicleNumber,
                v.ChassisNumber,
                v.EngineNumber,
                v.Make,
                v.Model,
                v.Year,
                v.Color,
                v.VehicleType,
                v.FuelType,
                v.SpeedLimitKmh,
                v.IdleAlertMinutes,
                v.CreatedAt,
                Imei = v.DeviceAssignments
                    .Where(a => a.Status == Enums.AssignmentStatus.Active)
                    .Select(a => a.Device.ImeiNumber).FirstOrDefault(),
                BoundAt = v.DeviceAssignments
                    .Where(a => a.Status == Enums.AssignmentStatus.Active)
                    .Select(a => (DateTime?)a.AssignedAt).FirstOrDefault(),
            })
            .ToListAsync();

        var vehicleIds = vehicleRows.Select(v => v.VehicleId).ToList();
        var plateById = vehicleRows.ToDictionary(v => v.VehicleId, v => v.VehicleNumber);

        string Plate(Guid vehicleId) => plateById.TryGetValue(vehicleId, out var p) ? p : string.Empty;

        // ── Everything else, each a single query ──────────────────────────────────────
        var reminders = await _db.VehicleReminders.AsNoTracking()
            .Where(r => vehicleIds.Contains(r.VehicleId))
            .OrderBy(r => r.DueDate)
            .Select(r => new { r.VehicleId, r.Type, r.DueDate, r.Notes })
            .ToListAsync();

        var places = await _db.SavedPlaces.AsNoTracking()
            .Where(p => p.CustomerId == id).OrderBy(p => p.Name).ToListAsync();

        var geofences = await _db.Geofences.AsNoTracking()
            .Where(g => g.CustomerId == id).OrderBy(g => g.Name).ToListAsync();

        var contacts = await _db.EmergencyContacts.AsNoTracking()
            .Where(c => c.CustomerId == id).OrderBy(c => c.Name).ToListAsync();

        var complaints = await _db.Complaints.AsNoTracking()
            .Where(c => c.CustomerId == id)
            .Include(c => c.Replies)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        var renewals = await _db.RenewalRequests.AsNoTracking()
            .Where(r => r.CustomerId == id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        var sharesGiven = await _db.VehicleShares.AsNoTracking()
            .Where(s => s.OwnerCustomerId == id)
            .Select(s => new { s.VehicleId, s.SharedWithCustomer.PhoneNumber, s.Status, s.InvitedAt, s.RespondedAt })
            .ToListAsync();

        var sharesReceived = await _db.VehicleShares.AsNoTracking()
            .Where(s => s.SharedWithCustomerId == id)
            .Select(s => new { Plate = s.Vehicle.VehicleNumber, s.OwnerCustomer.PhoneNumber, s.Status, s.InvitedAt, s.RespondedAt })
            .ToListAsync();

        var since = now - AlertWindow;
        var alerts = await _db.Alerts.AsNoTracking()
            .Where(a => vehicleIds.Contains(a.VehicleId) && a.TriggeredAt >= since)
            .OrderByDescending(a => a.TriggeredAt)
            .Take(MaxAlerts)
            .Select(a => new { a.VehicleId, a.AlertType, a.Message, a.Latitude, a.Longitude, a.TriggeredAt })
            .ToListAsync();

        var pushDevices = await _db.CustomerFcmTokens.AsNoTracking()
            .Where(t => t.CustomerId == id)
            .Select(t => new { t.Platform, t.UpdatedAt })
            .ToListAsync();

        var activeLinks = await _db.LiveShareLinks.AsNoTracking()
            .CountAsync(l => l.CustomerId == id && l.RevokedAt == null && l.ExpiresAt > now);

        // Plates for the geofence list (a geofence may be tied to a vehicle).
        var geofenceVehicleIds = geofences.Where(g => g.VehicleId.HasValue).Select(g => g.VehicleId!.Value).Distinct().ToList();
        var geofencePlates = geofenceVehicleIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Vehicles.AsNoTracking()
                .Where(v => geofenceVehicleIds.Contains(v.VehicleId))
                .ToDictionaryAsync(v => v.VehicleId, v => v.VehicleNumber);

        var renewalPlates = await _db.Vehicles.AsNoTracking()
            .Where(v => renewals.Select(r => r.VehicleId).Distinct().Contains(v.VehicleId))
            .ToDictionaryAsync(v => v.VehicleId, v => v.VehicleNumber);

        var complaintPlates = await _db.Vehicles.AsNoTracking()
            .Where(v => complaints.Select(c => c.VehicleId).Distinct().Contains(v.VehicleId))
            .ToDictionaryAsync(v => v.VehicleId, v => v.VehicleNumber);

        var sharedPlates = sharesGiven.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.Vehicles.AsNoTracking()
                .Where(v => sharesGiven.Select(s => s.VehicleId).Distinct().Contains(v.VehicleId))
                .ToDictionaryAsync(v => v.VehicleId, v => v.VehicleNumber);

        string PlateOf(Dictionary<Guid, string> map, Guid vehicleId) =>
            map.TryGetValue(vehicleId, out var p) ? p : string.Empty;

        var export = new AccountExportDto
        {
            GeneratedAtUtc = now,
            Note = "This file contains the personal data ShaloTrack holds about you. Push notification tokens " +
                   "and bank-slip images are not included. Your location history is available per vehicle and " +
                   "date range from Trip History (PDF/CSV export); contact support if you need a raw file.",
            Profile = new ExportProfileDto
            {
                CustomerId = customer.CustomerId,
                FullName = customer.FullName,
                Email = customer.Email,
                PhoneNumber = customer.PhoneNumber,
                NicNumber = customer.NicNumber,
                Address = customer.Address,
                AccountStatus = customer.AccountStatus.ToString(),
                WeeklySummaryEnabled = customer.WeeklySummaryEnabled,
                CreatedAt = customer.CreatedAt,
            },
            Vehicles = vehicleRows.Select(v => new ExportVehicleDto
            {
                VehicleId = v.VehicleId,
                VehicleNumber = v.VehicleNumber,
                ChassisNumber = v.ChassisNumber,
                EngineNumber = v.EngineNumber,
                Make = v.Make,
                Model = v.Model,
                Year = v.Year,
                Color = v.Color,
                VehicleType = v.VehicleType,
                FuelType = v.FuelType,
                SpeedLimitKmh = v.SpeedLimitKmh ?? Constants.AlertDefaults.SpeedLimitKmh,
                IdleAlertMinutes = v.IdleAlertMinutes,
                DeviceImei = v.Imei,
                DeviceBoundAt = v.BoundAt,
                CreatedAt = v.CreatedAt,
            }).ToList(),
            Reminders = reminders.Select(r => new ExportReminderDto
            {
                VehicleNumber = Plate(r.VehicleId),
                Type = r.Type.ToString(),
                DueDate = r.DueDate.ToString("yyyy-MM-dd"),
                Notes = r.Notes,
            }).ToList(),
            SavedPlaces = places.Select(p => new ExportPlaceDto
            {
                Name = p.Name,
                Latitude = p.Latitude,
                Longitude = p.Longitude,
                RadiusMeters = p.RadiusMeters,
                VisitCount = p.VisitCount,
                LastVisitedAt = p.LastVisitedAt,
                CreatedAt = p.CreatedAt,
            }).ToList(),
            Geofences = geofences.Select(g => new ExportGeofenceDto
            {
                Name = g.Name,
                VehicleNumber = g.VehicleId.HasValue ? PlateOf(geofencePlates, g.VehicleId.Value) : null,
                Latitude = g.Latitude,
                Longitude = g.Longitude,
                RadiusMeters = g.RadiusMeters,
                AlertOnEnter = g.AlertOnEnter,
                AlertOnExit = g.AlertOnExit,
                IsActive = g.IsActive,
                CreatedAt = g.CreatedAt,
            }).ToList(),
            EmergencyContacts = contacts.Select(c => new ExportEmergencyContactDto
            {
                Name = c.Name,
                PhoneNumber = c.PhoneNumber,
                Relationship = c.Relationship,
                CreatedAt = c.CreatedAt,
            }).ToList(),
            Complaints = complaints.Select(c => new ExportComplaintDto
            {
                VehicleNumber = PlateOf(complaintPlates, c.VehicleId),
                Category = c.Category.ToString(),
                Status = c.Status.ToString(),
                Description = c.Description,
                CreatedAt = c.CreatedAt,
                ResolvedAt = c.ResolvedAt,
                Replies = c.Replies.OrderBy(r => r.CreatedAt).Select(r => new ExportComplaintReplyDto
                {
                    // Staff names are not the customer's data: only the role is shown.
                    From = r.AuthorType == Enums.ComplaintReplyAuthorType.Customer ? "You" : r.AuthorType.ToString(),
                    Message = r.Message,
                    CreatedAt = r.CreatedAt,
                }).ToList(),
            }).ToList(),
            Renewals = renewals.Select(r => new ExportRenewalDto
            {
                VehicleNumber = PlateOf(renewalPlates, r.VehicleId),
                Duration = r.Duration.ToString(),
                PaymentMethod = r.PaymentMethod.ToString(),
                Status = r.Status.ToString(),
                AmountLkr = r.AmountLkr,
                PaymentReference = r.PaymentReference,
                CustomerNote = r.CustomerNote,
                SlipUploaded = r.SlipUploadedAt.HasValue,
                DecisionReason = r.DecisionReason,
                CreatedAt = r.CreatedAt,
                DecidedAt = r.DecidedAt,
            }).ToList(),
            VehicleSharesGiven = sharesGiven.Select(s => new ExportShareDto
            {
                VehicleNumber = PlateOf(sharedPlates, s.VehicleId),
                OtherPartyPhoneMasked = MaskPhone(s.PhoneNumber),
                Status = s.Status.ToString(),
                InvitedAt = s.InvitedAt,
                RespondedAt = s.RespondedAt,
            }).ToList(),
            VehicleSharesReceived = sharesReceived.Select(s => new ExportShareDto
            {
                VehicleNumber = s.Plate,
                OtherPartyPhoneMasked = MaskPhone(s.PhoneNumber),
                Status = s.Status.ToString(),
                InvitedAt = s.InvitedAt,
                RespondedAt = s.RespondedAt,
            }).ToList(),
            RecentAlerts = alerts.Select(a => new ExportAlertDto
            {
                VehicleNumber = Plate(a.VehicleId),
                AlertType = a.AlertType.ToString(),
                Message = a.Message,
                Latitude = a.Latitude,
                Longitude = a.Longitude,
                TriggeredAt = a.TriggeredAt,
            }).ToList(),
            Notifications = new ExportDevicesDto
            {
                PushDevices = pushDevices.Select(t => new ExportPushDeviceDto { Platform = t.Platform, LastSeenAt = t.UpdatedAt }).ToList(),
            },
            ActiveLiveLinks = activeLinks,
        };

        return ApiResponse<AccountExportDto>.Ok(export, "Your data export is ready.");
    }

    /// <summary>Shows only the last 3 digits, e.g. "*******123".</summary>
    internal static string MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var p = phone.Trim();
        return p.Length <= 3 ? new string('*', p.Length) : new string('*', p.Length - 3) + p[^3..];
    }

    private static ApiResponse<AccountExportDto> Unauthorized() =>
        ApiResponse<AccountExportDto>.Fail(
            (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
}