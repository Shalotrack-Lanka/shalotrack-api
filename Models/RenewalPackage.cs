using System.ComponentModel.DataAnnotations;
using ShaloTrack_API.Enums;

namespace ShaloTrack_API.Models;

/// <summary>
/// A read-only copy of one row of the admin portal's renewal price master (the admin database is the
/// single source of truth and pushes changes here). It holds only what the mobile app and the
/// renewal flow need: the price the customer pays and the warranty shown. Dealer, distributor and
/// company margins are deliberately NOT stored here; they never leave the admin portal.
/// </summary>
public class RenewalPackage
{
    /// <summary>THREE_MONTHS | SIX_MONTHS | ONE_YEAR | TWO_YEARS | THREE_YEARS | SIX_YEARS</summary>
    [Key]
    [MaxLength(32)]
    public string Code { get; set; } = string.Empty;

    public RenewalDuration Duration { get; set; }

    [MaxLength(40)]
    public string Label { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? Positioning { get; set; }

    public int Months { get; set; }

    /// <summary>What the customer pays. Null = not priced yet, so the package cannot be offered.</summary>
    public decimal? CustomerPriceLkr { get; set; }

    public int WarrantyMonths { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }

    /// <summary>When the admin portal last pushed this row.</summary>
    public DateTime SyncedAt { get; set; }

    /// <summary>A package can be offered to customers only when it is active and priced.</summary>
    public bool IsOffered => IsActive && CustomerPriceLkr is > 0;
}