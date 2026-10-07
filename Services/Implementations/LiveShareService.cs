using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.LiveShare;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Temporary live-location links.
///
/// Owner side: only the vehicle's owner can create / list / stop links; anything else answers 404
/// (never 403). Public side: anyone holding a valid token can read ONE vehicle's plate, position
/// and the trail since the link was created, and nothing else.
///
/// Security notes:
///  - 256-bit random token; only its SHA-256 hash is stored.
///  - Every failure on the public side is the same 404 with the same text, so a caller cannot tell
///    "never existed" from "expired" from "stopped".
///  - The trail starts at the link's creation time, never earlier.
///  - A vehicle whose subscription has lapsed, or that is inactive / the demo vehicle, is not served.
/// </summary>
public class LiveShareService : ILiveShareService
{
    public const int MinHours = 1;
    public const int MaxHours = 24;
    public const int MaxActiveLinksPerVehicle = 3;

    private const int TokenBytes = 32;                 // 256 bits
    private const int TokenChars = 43;                 // base64url length of 32 bytes, no padding
    private const int TrailMaxRowsRead = 3000;
    private const int TrailMaxPointsReturned = 400;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILiveShareLinkRepository _links;
    private readonly ICurrentUser _currentUser;
    private readonly ISubscriptionGateService _subscriptionGate;

    public LiveShareService(
        IUnitOfWork unitOfWork,
        ILiveShareLinkRepository links,
        ICurrentUser currentUser,
        ISubscriptionGateService subscriptionGate)
    {
        _unitOfWork = unitOfWork;
        _links = links;
        _currentUser = currentUser;
        _subscriptionGate = subscriptionGate;
    }

    // ───────────────────────── Owner side ─────────────────────────

    public async Task<ApiResponse<LiveShareLinkResponseDto>> CreateAsync(Guid vehicleId, CreateLiveShareDto dto)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
            return ApiResponse<LiveShareLinkResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");

        if (dto.DurationHours < MinHours || dto.DurationHours > MaxHours)
            return ApiResponse<LiveShareLinkResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Invalid duration.",
                $"Duration must be between {MinHours} and {MaxHours} hours.");

        var vehicle = await _unitOfWork.Vehicles.GetByIdForOwnershipCheckAsync(vehicleId);
        if (!Owns(customer, vehicle))
            return OwnerNotFound<LiveShareLinkResponseDto>();

        if (await _subscriptionGate.IsRenewalRequiredAsync(vehicleId))
            return ApiResponse<LiveShareLinkResponseDto>.Fail(
                (int)HttpStatusCode.PaymentRequired, "Subscription renewal required.",
                ISubscriptionGateService.RenewalRequiredCode);

        if (await _links.GetLocationAsync(vehicleId) is null)
            return ApiResponse<LiveShareLinkResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "No live location yet.",
                "This vehicle has not reported a location yet.");

        var now = DateTime.UtcNow;
        if (await _links.CountActiveAsync(vehicleId, now) >= MaxActiveLinksPerVehicle)
            return ApiResponse<LiveShareLinkResponseDto>.Fail(
                (int)HttpStatusCode.Conflict, "Too many active links.",
                $"This vehicle already has {MaxActiveLinksPerVehicle} active links. Stop one first.");

        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(TokenBytes));

        var link = new LiveShareLink
        {
            LinkId = Guid.NewGuid(),
            VehicleId = vehicleId,
            CustomerId = customer.CustomerId,
            TokenHash = Hash(token),
            CreatedAt = now,
            ExpiresAt = now.AddHours(dto.DurationHours),
        };

        await _links.AddAsync(link);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<LiveShareLinkResponseDto>.Ok(ToDto(link, token), "Live link created.");
    }

    public async Task<ApiResponse<IReadOnlyList<LiveShareLinkResponseDto>>> ListActiveAsync(Guid vehicleId)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
            return ApiResponse<IReadOnlyList<LiveShareLinkResponseDto>>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");

        var vehicle = await _unitOfWork.Vehicles.GetByIdForOwnershipCheckAsync(vehicleId);
        if (!Owns(customer, vehicle))
            return OwnerNotFound<IReadOnlyList<LiveShareLinkResponseDto>>();

        var rows = await _links.GetActiveByVehicleAsync(vehicleId, DateTime.UtcNow);

        return ApiResponse<IReadOnlyList<LiveShareLinkResponseDto>>.Ok(
            rows.Select(r => ToDto(r, null)).ToList(), "Active links retrieved successfully.");
    }

    public async Task<ApiResponse<string>> RevokeAsync(Guid linkId)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");

        var link = await _links.GetByIdAsync(linkId);
        if (link is null || link.CustomerId != customer.CustomerId)
            return OwnerNotFound<string>();

        if (link.RevokedAt is null)
        {
            link.RevokedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
        }

        return ApiResponse<string>.Ok("Stopped", "Live link stopped.");
    }

    // ───────────────────────── Public side ─────────────────────────

    public async Task<ApiResponse<PublicLiveViewDto>> GetPublicViewAsync(string token, bool includeTrail)
    {
        // Cheap shape check first: garbage never reaches the database.
        if (!LooksLikeToken(token))
            return PublicNotFound();

        var link = await _links.GetByTokenHashAsync(Hash(token));
        var now = DateTime.UtcNow;
        if (link is null || link.RevokedAt is not null || link.ExpiresAt <= now)
            return PublicNotFound();

        var vehicle = await _links.GetVehicleInfoAsync(link.VehicleId);
        if (vehicle is null || !vehicle.IsActive || vehicle.IsDemoVehicle)
            return PublicNotFound();

        if (await _subscriptionGate.IsRenewalRequiredAsync(link.VehicleId))
            return PublicNotFound();

        var location = await _links.GetLocationAsync(link.VehicleId);
        if (location is null)
            return PublicNotFound();

        var view = new PublicLiveViewDto
        {
            PlateNumber = vehicle.VehicleNumber,
            ExpiresAt = link.ExpiresAt,
            ServerTime = now,
            Position = new PublicLivePositionDto
            {
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                SpeedKmh = location.Speed,
                Heading = location.Heading,
                IsMoving = location.MovementStatus,
                LastUpdate = location.LastUpdate,
            },
        };

        if (includeTrail)
        {
            // From the moment the link was created, never earlier.
            var rows = await _links.GetTrailAsync(location.DeviceId, link.CreatedAt, TrailMaxRowsRead);
            view.Trail = Downsample(rows)
                .Select(r => new PublicLiveTrailPointDto
                {
                    Latitude = r.Latitude,
                    Longitude = r.Longitude,
                    Time = r.EventTime,
                })
                .ToList();
        }

        return ApiResponse<PublicLiveViewDto>.Ok(view, "Live view retrieved successfully.");
    }

    // ───────────────────────── helpers ─────────────────────────

    private static bool LooksLikeToken(string? token)
    {
        if (token is null || token.Length != TokenChars) return false;
        foreach (var c in token)
        {
            var ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
            if (!ok) return false;
        }
        return true;
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    /// <summary>Keeps at most TrailMaxPointsReturned points, evenly spread, always including the newest.</summary>
    private static List<TrailRow> Downsample(List<TrailRow> rows)
    {
        if (rows.Count <= TrailMaxPointsReturned) return rows;

        var step = (double)(rows.Count - 1) / (TrailMaxPointsReturned - 1);
        var result = new List<TrailRow>(TrailMaxPointsReturned);
        for (var i = 0; i < TrailMaxPointsReturned; i++)
            result.Add(rows[(int)Math.Round(i * step)]);
        return result;
    }

    private static bool Owns(Customer customer, Vehicle? vehicle) =>
        vehicle is not null
        && vehicle.IsActive
        && !vehicle.IsDemoVehicle
        && vehicle.CustomerId == customer.CustomerId;

    private static ApiResponse<T> OwnerNotFound<T>() =>
        ApiResponse<T>.Fail((int)HttpStatusCode.NotFound, "Not found.", "No such item.");

    // One answer for every public failure: unknown, malformed, expired, stopped, lapsed.
    private static ApiResponse<PublicLiveViewDto> PublicNotFound() =>
        ApiResponse<PublicLiveViewDto>.Fail(
            (int)HttpStatusCode.NotFound, "This link has expired or is no longer available.", "Not found.");

    private async Task<Customer?> ResolveCustomerAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid)) return null;
        return await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
    }

    private static LiveShareLinkResponseDto ToDto(LiveShareLink l, string? token) => new()
    {
        LinkId = l.LinkId,
        CreatedAt = l.CreatedAt,
        ExpiresAt = l.ExpiresAt,
        Token = token,
    };
}