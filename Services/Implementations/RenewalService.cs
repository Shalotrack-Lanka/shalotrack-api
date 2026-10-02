using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Auth;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.Renewals;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Extensions;
using ShaloTrack_API.Models;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Customer-initiated device renewals, paid by bank transfer with a slip (a payment gateway can be
/// added later as another RenewalPaymentMethod without touching the rest).
///
/// Security model:
///  - A customer can only ever act on requests for vehicles THEY own; a wrong id looks exactly like
///    a missing one (404), so ids cannot be probed.
///  - This service never writes the subscription. Staff do that in the admin portal and then call
///    DecideAsync; a request approved here changes no access by itself.
///  - Approval must quote the SHA-256 of the slip staff looked at, so a slip swapped mid-review
///    cannot be approved by accident.
/// </summary>
public class RenewalService : IRenewalService
{
    // Only allow a renewal request this close to the end (or when lapsed / never paid). Tunable.
    public const int RenewalWindowDays = 60;

    // An abandoned "awaiting slip" request stops blocking new ones after this long.
    public const int AwaitingSlipExpiryDays = 7;

    public const int MaxSlipBytes = 2 * 1024 * 1024;
    private const int MaxReasonLength = 300;
    private const int MaxReferenceLength = 100;
    private const int MaxNoteLength = 300;

    private readonly ShaloTrackDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IPushNotificationService _push;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RenewalService> _logger;

    public RenewalService(
        ShaloTrackDbContext db,
        ICurrentUser currentUser,
        IPushNotificationService push,
        IConfiguration configuration,
        ILogger<RenewalService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _push = push;
        _configuration = configuration;
        _logger = logger;
    }

    // ------------------------------------------------------------------ customer

    public async Task<ApiResponse<RenewalResponseDto>> CreateAsync(CreateRenewalDto dto)
    {
        var customer = await GetCallerAsync();
        if (customer is null) return NotAuthenticatedOrMissing<RenewalResponseDto>();

        if (!RenewalEnumExtensions.TryParseStrict<RenewalDuration>(dto.Duration, out var duration))
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.BadRequest, "Invalid duration.",
                "Expected ThreeMonths, SixMonths, OneYear, TwoYears, ThreeYears or SixYears.");
        }

        var method = RenewalPaymentMethod.BankSlip;
        if (!string.IsNullOrWhiteSpace(dto.PaymentMethod)
            && !RenewalEnumExtensions.TryParseStrict(dto.PaymentMethod, out method))
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.BadRequest, "Invalid payment method.",
                "Expected BankSlip.");
        }
        if (method == RenewalPaymentMethod.Gateway)
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.BadRequest, "Online payment is not available yet.",
                "Pay by bank transfer and upload the slip.");
        }

        var reference = Trim(dto.PaymentReference, MaxReferenceLength);
        var note = Trim(dto.CustomerNote, MaxNoteLength);
        if ((dto.PaymentReference?.Trim().Length ?? 0) > MaxReferenceLength || (dto.CustomerNote?.Trim().Length ?? 0) > MaxNoteLength)
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.BadRequest, "Text too long.",
                $"Reference is limited to {MaxReferenceLength} characters and the note to {MaxNoteLength}.");
        }

        // Owner only, active only. Same 404 for "not yours" and "does not exist".
        var vehicle = await _db.Vehicles.AsNoTracking()
            .FirstOrDefaultAsync(v => v.VehicleId == dto.VehicleId && v.CustomerId == customer.CustomerId && v.IsActive);
        if (vehicle is null)
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.NotFound, "Vehicle not found.",
                "No vehicle of yours matches that id.");
        }
        if (vehicle.IsDemoVehicle)
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.BadRequest, "Nothing to renew.",
                "The demo vehicle does not need a subscription.");
        }

        var imei = await _db.DeviceAssignments.AsNoTracking()
            .Where(a => a.VehicleId == vehicle.VehicleId && a.Status == AssignmentStatus.Active)
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => a.Device.ImeiNumber)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(imei))
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.BadRequest, "No device on this vehicle.",
                "A renewal needs a tracking device linked to the vehicle.");
        }

        var now = DateTime.UtcNow;

        // The price is fixed here, on the server, from the admin portal's price list. The app never sends
        // an amount, so a customer cannot pick their own price.
        var (amount, priceError) = await ResolvePriceAsync(duration);
        if (priceError is not null)
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.BadRequest, "This package is not available.", priceError);
        }

        var gate = await _db.DeviceSubscriptionStatuses.AsNoTracking().FirstOrDefaultAsync(d => d.ImeiNumber == imei);
        if (gate is { IsActive: true, ExpiresAt: not null } && gate.ExpiresAt.Value > now.AddDays(RenewalWindowDays))
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.Conflict, "Too early to renew.",
                $"Your subscription runs until {gate.ExpiresAt.Value:yyyy-MM-dd}. You can renew within {RenewalWindowDays} days of the end date.");
        }

        // An abandoned request that never got a slip must not block the vehicle forever.
        var staleBefore = now.AddDays(-AwaitingSlipExpiryDays);
        // DbContext is NoTracking globally: AsTracking() is required or the changes below are silently not saved.
        var stale = await _db.RenewalRequests.AsTracking()
            .Where(r => r.VehicleId == vehicle.VehicleId && r.Status == RenewalStatus.AwaitingSlip && r.CreatedAt < staleBefore)
            .ToListAsync();
        foreach (var s in stale)
        {
            s.Status = RenewalStatus.Cancelled;
            s.DecisionReason = "Expired: no payment slip was uploaded.";
            s.UpdatedAt = now;
        }
        if (stale.Count > 0) await _db.SaveChangesAsync();

        var open = await _db.RenewalRequests.AsNoTracking()
            .FirstOrDefaultAsync(r => r.VehicleId == vehicle.VehicleId
                && (r.Status == RenewalStatus.AwaitingSlip || r.Status == RenewalStatus.PendingReview));
        if (open is not null)
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.Conflict, "A renewal request is already open for this vehicle.",
                open.Status == RenewalStatus.AwaitingSlip
                    ? "Upload your payment slip on the existing request, or cancel it and start again."
                    : "It is waiting for our team to review the slip you uploaded.");
        }

        var request = new RenewalRequest
        {
            RenewalRequestId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            VehicleId = vehicle.VehicleId,
            ImeiNumber = imei,
            Duration = duration,
            AmountLkr = amount,
            PaymentMethod = method,
            Status = RenewalStatus.AwaitingSlip,
            PaymentReference = reference,
            CustomerNote = note,
            CreatedAt = now,
            UpdatedAt = now,
        };

        try
        {
            await _db.RenewalRequests.AddAsync(request);
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            // The unique "one open request per vehicle" index caught a concurrent double-tap.
            _logger.LogInformation(ex, "Renewal create hit the open-request guard for vehicle {VehicleId}.", vehicle.VehicleId);
            _db.ChangeTracker.Clear();
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.Conflict, "A renewal request is already open for this vehicle.",
                "Open the existing request to continue.");
        }

        var response = ToDto(request, vehicle.VehicleNumber);
        response.InstructionsMessage = _configuration["Renewals:PaymentInstructions"]
            ?? "Pay by bank transfer, then upload a clear photo or PDF of the transfer slip. Our team will review it and update your subscription.";

        return ApiResponse<RenewalResponseDto>.Ok(response, "Renewal request created.");
    }

    public async Task<ApiResponse<RenewalResponseDto>> UploadSlipAsync(Guid renewalRequestId, IFormFile file)
    {
        var customer = await GetCallerAsync();
        if (customer is null) return NotAuthenticatedOrMissing<RenewalResponseDto>();

        if (file is null || file.Length == 0)
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.BadRequest, "No file received.", "Attach the slip as a file.");
        }
        if (file.Length > MaxSlipBytes)
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.RequestEntityTooLarge, "File too large.",
                $"The slip must be {MaxSlipBytes / (1024 * 1024)} MB or smaller.");
        }

        // DbContext is NoTracking globally: AsTracking() is required or the changes below are silently not saved.
        var request = await _db.RenewalRequests.AsTracking()
            .Include(r => r.Slip)
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.RenewalRequestId == renewalRequestId && r.CustomerId == customer.CustomerId);
        if (request is null) return RequestNotFound<RenewalResponseDto>();

        if (request.Status != RenewalStatus.AwaitingSlip && request.Status != RenewalStatus.PendingReview)
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.Conflict, "This request is closed.",
                $"A {request.Status} request cannot take a new slip. Start a new renewal request.");
        }

        byte[] content;
        await using (var stream = file.OpenReadStream())
        {
            // Read at most one byte more than allowed, so a lying Content-Length cannot exhaust memory.
            using var ms = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                ms.Write(buffer, 0, read);
                if (ms.Length > MaxSlipBytes)
                {
                    return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.RequestEntityTooLarge, "File too large.",
                        $"The slip must be {MaxSlipBytes / (1024 * 1024)} MB or smaller.");
                }
            }
            content = ms.ToArray();
        }

        // Decide the type from the bytes themselves; the client's claimed type is not trusted.
        var contentType = DetectSlipContentType(content);
        if (contentType is null)
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.UnsupportedMediaType, "Unsupported file.",
                "Upload a JPEG or PNG photo, or a PDF.");
        }

        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var now = DateTime.UtcNow;

        if (request.Slip is null)
        {
            request.Slip = new RenewalSlip { RenewalRequestId = request.RenewalRequestId };
            _db.RenewalSlips.Add(request.Slip);
        }
        request.Slip.Content = content;
        request.Slip.ContentType = contentType;
        request.Slip.SizeBytes = content.Length;
        request.Slip.Sha256 = hash;
        request.Slip.UploadedAt = now;

        request.SlipSha256 = hash;
        request.SlipUploadedAt = now;
        request.Status = RenewalStatus.PendingReview;
        request.UpdatedAt = now;

        await _db.SaveChangesAsync();

        var dto = ToDto(request, request.Vehicle.VehicleNumber);
        dto.InstructionsMessage = "Slip received. Our team will review it and update your subscription.";
        return ApiResponse<RenewalResponseDto>.Ok(dto, "Slip uploaded.");
    }

    public async Task<ApiResponse<IReadOnlyList<RenewalResponseDto>>> GetMineAsync()
    {
        var customer = await GetCallerAsync();
        if (customer is null) return NotAuthenticatedOrMissing<IReadOnlyList<RenewalResponseDto>>();

        var rows = await _db.RenewalRequests.AsNoTracking()
            .Where(r => r.CustomerId == customer.CustomerId)
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .Select(r => new
            {
                Request = r,
                r.Vehicle.VehicleNumber,
                HasSlip = r.SlipSha256 != null,
            })
            .ToListAsync();

        IReadOnlyList<RenewalResponseDto> list = rows.Select(x => ToDto(x.Request, x.VehicleNumber)).ToList();
        return ApiResponse<IReadOnlyList<RenewalResponseDto>>.Ok(list, "Renewal requests retrieved.");
    }

    public async Task<ApiResponse<RenewalResponseDto>> CancelAsync(Guid renewalRequestId)
    {
        var customer = await GetCallerAsync();
        if (customer is null) return NotAuthenticatedOrMissing<RenewalResponseDto>();

        // DbContext is NoTracking globally: AsTracking() is required or the changes below are silently not saved.
        var request = await _db.RenewalRequests.AsTracking()
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.RenewalRequestId == renewalRequestId && r.CustomerId == customer.CustomerId);
        if (request is null) return RequestNotFound<RenewalResponseDto>();

        if (request.Status != RenewalStatus.AwaitingSlip && request.Status != RenewalStatus.PendingReview)
        {
            return ApiResponse<RenewalResponseDto>.Fail((int)HttpStatusCode.Conflict, "Cannot cancel.",
                $"This request is already {request.Status}.");
        }

        request.Status = RenewalStatus.Cancelled;
        request.DecisionReason = "Cancelled by the customer.";
        request.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ApiResponse<RenewalResponseDto>.Ok(ToDto(request, request.Vehicle.VehicleNumber), "Renewal request cancelled.");
    }

    // ------------------------------------------------------------------ staff (internal routes)

    public async Task<ApiResponse<IReadOnlyList<InternalRenewalDto>>> GetForAdminAsync(string? status, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsStaff) return Forbidden<IReadOnlyList<InternalRenewalDto>>();

        var query = _db.RenewalRequests.AsNoTracking().AsQueryable();

        if (string.IsNullOrWhiteSpace(status) || status.Equals("PendingReview", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.Status == RenewalStatus.PendingReview);
        }
        else if (!status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (!RenewalEnumExtensions.TryParseStrict<RenewalStatus>(status, out var parsed))
            {
                return ApiResponse<IReadOnlyList<InternalRenewalDto>>.Fail((int)HttpStatusCode.BadRequest, "Invalid status.",
                    "Expected PendingReview, AwaitingSlip, Approved, Rejected, Cancelled or all.");
            }
            query = query.Where(r => r.Status == parsed);
        }

        var rows = await query
            .OrderBy(r => r.Status == RenewalStatus.PendingReview ? r.SlipUploadedAt : r.UpdatedAt)
            .ThenBy(r => r.CreatedAt)
            .Take(200)
            .Select(r => new InternalRenewalDto
            {
                RenewalRequestId = r.RenewalRequestId,
                Status = r.Status.ToString(),
                Duration = r.Duration.ToString(),
                PaymentMethod = r.PaymentMethod.ToString(),
                AmountLkr = r.AmountLkr,
                ImeiNumber = r.ImeiNumber,
                VehicleId = r.VehicleId,
                VehicleNumber = r.Vehicle.VehicleNumber,
                CustomerName = r.Customer.FullName,
                CustomerPhone = r.Customer.PhoneNumber,
                PaymentReference = r.PaymentReference,
                CustomerNote = r.CustomerNote,
                HasSlip = r.SlipSha256 != null,
                SlipSha256 = r.SlipSha256,
                SlipUploadedAt = r.SlipUploadedAt,
                DecisionReason = r.DecisionReason,
                DecidedAt = r.DecidedAt,
                DecidedBy = r.DecidedBy,
                CreatedAt = r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        foreach (var r in rows)
        {
            r.DurationModel = Enum.Parse<RenewalDuration>(r.Duration).ToAdminModel();
        }

        return ApiResponse<IReadOnlyList<InternalRenewalDto>>.Ok(rows, "Renewal requests retrieved.");
    }

    public async Task<(byte[] Content, string ContentType)?> GetSlipForAdminAsync(Guid renewalRequestId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsStaff) return null;

        var slip = await _db.RenewalSlips.AsNoTracking()
            .Where(s => s.RenewalRequestId == renewalRequestId)
            .Select(s => new { s.Content, s.ContentType })
            .FirstOrDefaultAsync(cancellationToken);

        return slip is null ? null : (slip.Content, slip.ContentType);
    }

    public async Task<ApiResponse<InternalRenewalDto>> DecideAsync(Guid renewalRequestId, DecideRenewalDto dto)
    {
        if (!_currentUser.IsStaff) return Forbidden<InternalRenewalDto>();

        var decision = (dto.Decision ?? string.Empty).Trim().ToLowerInvariant();
        if (decision is not ("approve" or "reject"))
        {
            return ApiResponse<InternalRenewalDto>.Fail((int)HttpStatusCode.BadRequest, "Invalid decision.", "Expected approve or reject.");
        }

        var reason = Trim(dto.Reason, MaxReasonLength);
        if (decision == "reject" && string.IsNullOrWhiteSpace(reason))
        {
            return ApiResponse<InternalRenewalDto>.Fail((int)HttpStatusCode.BadRequest, "A reason is required.", "Say why the renewal is not approved.");
        }
        if ((dto.Reason?.Trim().Length ?? 0) > MaxReasonLength)
        {
            return ApiResponse<InternalRenewalDto>.Fail((int)HttpStatusCode.BadRequest, "Reason too long.", $"Limit is {MaxReasonLength} characters.");
        }

        // DbContext is NoTracking globally: AsTracking() is required or the changes below are silently not saved.
        var request = await _db.RenewalRequests.AsTracking()
            .Include(r => r.Vehicle)
            .Include(r => r.Customer)
            .FirstOrDefaultAsync(r => r.RenewalRequestId == renewalRequestId);
        if (request is null) return RequestNotFound<InternalRenewalDto>();

        if (request.Status != RenewalStatus.PendingReview)
        {
            return ApiResponse<InternalRenewalDto>.Fail((int)HttpStatusCode.Conflict, "Not waiting for review.",
                $"This request is {request.Status}.");
        }

        if (decision == "approve")
        {
            if (string.IsNullOrWhiteSpace(dto.SlipSha256)
                || !string.Equals(dto.SlipSha256.Trim(), request.SlipSha256, StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<InternalRenewalDto>.Fail((int)HttpStatusCode.Conflict, "The slip changed.",
                    "The customer replaced the slip after you opened it. Open the request again and check the new one.");
            }
        }

        var now = DateTime.UtcNow;
        request.Status = decision == "approve" ? RenewalStatus.Approved : RenewalStatus.Rejected;
        request.DecisionReason = decision == "reject" ? reason : null;
        request.DecidedAt = now;
        request.DecidedBy = Trim(dto.DecidedBy, 64);
        request.UpdatedAt = now;
        await _db.SaveChangesAsync();

        await NotifyCustomerAsync(request, decision == "approve");

        var result = new InternalRenewalDto
        {
            RenewalRequestId = request.RenewalRequestId,
            Status = request.Status.ToString(),
            Duration = request.Duration.ToString(),
            DurationModel = request.Duration.ToAdminModel(),
            PaymentMethod = request.PaymentMethod.ToString(),
            AmountLkr = request.AmountLkr,
            ImeiNumber = request.ImeiNumber,
            VehicleId = request.VehicleId,
            VehicleNumber = request.Vehicle.VehicleNumber,
            CustomerName = request.Customer.FullName,
            CustomerPhone = request.Customer.PhoneNumber,
            PaymentReference = request.PaymentReference,
            CustomerNote = request.CustomerNote,
            HasSlip = request.SlipSha256 != null,
            SlipSha256 = request.SlipSha256,
            SlipUploadedAt = request.SlipUploadedAt,
            DecisionReason = request.DecisionReason,
            DecidedAt = request.DecidedAt,
            DecidedBy = request.DecidedBy,
            CreatedAt = request.CreatedAt,
        };

        return ApiResponse<InternalRenewalDto>.Ok(result, decision == "approve" ? "Renewal approved." : "Renewal rejected.");
    }

    // ------------------------------------------------------------------ helpers

    private async Task NotifyCustomerAsync(RenewalRequest request, bool approved)
    {
        try
        {
            var vehicle = request.Vehicle.VehicleNumber;
            var title = approved ? "Renewal approved" : "Renewal not approved";
            var body = approved
                ? $"Your renewal for {vehicle} was approved. Your subscription has been updated."
                : $"Your renewal request for {vehicle} was not approved: {request.DecisionReason}";

            await _push.SendAlertPushAsync(request.CustomerId, title, body, new Dictionary<string, string>
            {
                ["type"] = "renewal_decision",
                ["status"] = request.Status.ToString(),
                ["renewalRequestId"] = request.RenewalRequestId.ToString(),
            });
        }
        catch (Exception ex)
        {
            // The decision is already saved; a failed push must never undo or fail it.
            _logger.LogError(ex, "Renewal decision push failed for request {RenewalRequestId}.", request.RenewalRequestId);
        }
    }

    /// <summary>
    /// The price to stamp on a new request. Until the admin portal has pushed a price list at all (first
    /// deployment), requests stay unpriced exactly as before, so renewals never stop because of a missed
    /// sync. Once a list exists, a package that is missing, inactive or unpriced cannot be ordered.
    /// </summary>
    private async Task<(decimal? Amount, string? Error)> ResolvePriceAsync(RenewalDuration duration)
    {
        const string notOffered = "This renewal package is not on offer right now. Choose another one or contact support.";

        var code = duration.ToPackageCode();
        var package = await _db.RenewalPackages.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code);
        if (package is not null)
        {
            if (!package.IsOffered) return (null, notOffered);
            return (package.CustomerPriceLkr, null);
        }

        if (await _db.RenewalPackages.AsNoTracking().AnyAsync())
        {
            return (null, notOffered);
        }

        _logger.LogWarning("No renewal price list has been synced yet; creating an unpriced renewal request.");
        return (null, null);
    }

    private async Task<Customer?> GetCallerAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid)) return null;

        return await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.FirebaseUid == uid);
    }

    /// <summary>JPEG, PNG or PDF by magic bytes; anything else is refused.</summary>
    internal static string? DetectSlipContentType(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47
            && b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        if (b.Length >= 5 && b[0] == 0x25 && b[1] == 0x50 && b[2] == 0x44 && b[3] == 0x46 && b[4] == 0x2D) return "application/pdf";
        return null;
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim();
        return v.Length > max ? v[..max] : v;
    }

    private static RenewalResponseDto ToDto(RenewalRequest r, string vehicleNumber) => new()
    {
        RenewalRequestId = r.RenewalRequestId,
        VehicleId = r.VehicleId,
        VehicleNumber = vehicleNumber,
        Duration = r.Duration.ToString(),
        PaymentMethod = r.PaymentMethod.ToString(),
        AmountLkr = r.AmountLkr,
        Status = r.Status.ToString(),
        HasSlip = r.SlipSha256 != null,
        PaymentReference = r.PaymentReference,
        DecisionReason = r.DecisionReason,
        CreatedAt = r.CreatedAt,
        DecidedAt = r.DecidedAt,
    };

    private static ApiResponse<T> NotAuthenticatedOrMissing<T>() =>
        ApiResponse<T>.Fail((int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");

    private static ApiResponse<T> RequestNotFound<T>() =>
        ApiResponse<T>.Fail((int)HttpStatusCode.NotFound, "Renewal request not found.", "No such renewal request.");

    private static ApiResponse<T> Forbidden<T>() =>
        ApiResponse<T>.Fail((int)HttpStatusCode.Forbidden, "Not authorized.", "Staff only.");
}