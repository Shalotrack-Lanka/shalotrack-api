using System.Net;
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
/// The renewal price list as the mobile app sees it. The admin portal owns the prices and pushes them
/// here; this service only stores that copy and serves the customer-safe part of it.
/// </summary>
public class RenewalPackageService : IRenewalPackageService
{
    public const decimal MaxPriceLkr = 1_000_000m;

    private readonly ShaloTrackDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<RenewalPackageService> _logger;

    public RenewalPackageService(ShaloTrackDbContext db, ICurrentUser currentUser, ILogger<RenewalPackageService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<ApiResponse<IReadOnlyList<RenewalPackageDto>>> GetOfferedAsync(CancellationToken cancellationToken)
    {
        var rows = await _db.RenewalPackages.AsNoTracking()
            .Where(p => p.IsActive && p.CustomerPriceLkr != null && p.CustomerPriceLkr > 0)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Months)
            .ToListAsync(cancellationToken);

        IReadOnlyList<RenewalPackageDto> list = rows.Select(p => new RenewalPackageDto
        {
            Duration = p.Duration.ToString(),
            Label = p.Label,
            Positioning = p.Positioning,
            Months = p.Months,
            PriceLkr = p.CustomerPriceLkr!.Value,
            WarrantyMonths = p.WarrantyMonths,
        }).ToList();

        return ApiResponse<IReadOnlyList<RenewalPackageDto>>.Ok(list, "Renewal packages retrieved.");
    }

    public async Task<ApiResponse<int>> SyncAsync(SyncRenewalPackagesDto dto, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsStaff)
        {
            return ApiResponse<int>.Fail((int)HttpStatusCode.Forbidden, "Not authorized.", "Staff only.");
        }

        var items = dto?.Packages ?? new List<SyncRenewalPackageItemDto>();
        if (items.Count == 0 || items.Count > 20)
        {
            return ApiResponse<int>.Fail((int)HttpStatusCode.BadRequest, "Invalid package list.", "Send between 1 and 20 packages.");
        }

        // Validate everything first: one bad row rejects the whole push, so the stored copy is never half-updated.
        var known = Enum.GetValues<RenewalDuration>().ToDictionary(d => d.ToPackageCode(), d => d);
        var parsed = new List<(SyncRenewalPackageItemDto Item, RenewalDuration Duration)>();
        var seen = new HashSet<string>();
        foreach (var item in items)
        {
            var code = (item.Code ?? string.Empty).Trim().ToUpperInvariant();
            if (!known.TryGetValue(code, out var duration))
            {
                return ApiResponse<int>.Fail((int)HttpStatusCode.BadRequest, "Unknown package.", $"'{item.Code}' is not a known package code.");
            }
            if (!seen.Add(code))
            {
                return ApiResponse<int>.Fail((int)HttpStatusCode.BadRequest, "Duplicate package.", $"'{code}' appears twice.");
            }
            if (string.IsNullOrWhiteSpace(item.Label) || item.Label.Trim().Length > 40
                || (item.Positioning?.Trim().Length ?? 0) > 120)
            {
                return ApiResponse<int>.Fail((int)HttpStatusCode.BadRequest, "Invalid text.", $"{code}: label is required (max 40) and positioning is max 120 characters.");
            }
            if (item.Months < 1 || item.Months > 120 || item.WarrantyMonths < 0 || item.WarrantyMonths > 120)
            {
                return ApiResponse<int>.Fail((int)HttpStatusCode.BadRequest, "Invalid months.", $"{code}: months must be 1-120 and warranty months 0-120.");
            }
            if (item.CustomerPrice is < 0 or > MaxPriceLkr)
            {
                return ApiResponse<int>.Fail((int)HttpStatusCode.BadRequest, "Invalid price.", $"{code}: price must be between 0 and {MaxPriceLkr:N0} LKR.");
            }
            parsed.Add((item, duration));
        }

        var now = DateTime.UtcNow;
        // DbContext is NoTracking globally: AsTracking() is required or the changes below are silently not saved.
        var existing = await _db.RenewalPackages.AsTracking().ToDictionaryAsync(p => p.Code, cancellationToken);

        foreach (var (item, duration) in parsed)
        {
            var code = duration.ToPackageCode();
            if (!existing.TryGetValue(code, out var row))
            {
                row = new RenewalPackage { Code = code };
                _db.RenewalPackages.Add(row);
            }

            row.Duration = duration;
            row.Label = item.Label.Trim();
            row.Positioning = string.IsNullOrWhiteSpace(item.Positioning) ? null : item.Positioning.Trim();
            row.Months = item.Months;
            row.CustomerPriceLkr = item.CustomerPrice is > 0 ? decimal.Round(item.CustomerPrice.Value, 2) : null;
            row.WarrantyMonths = item.WarrantyMonths;
            row.IsActive = item.IsActive;
            row.SortOrder = item.SortOrder;
            row.SyncedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Renewal price list synced from the admin portal: {Count} packages.", parsed.Count);

        return ApiResponse<int>.Ok(parsed.Count, "Renewal packages synced.");
    }
}