namespace ShaloTrack_API.DTOs.Renewals;

/// <summary>Pushed by the admin portal (internal route only). The full current list, not a delta.</summary>
public class SyncRenewalPackagesDto
{
    public List<SyncRenewalPackageItemDto> Packages { get; set; } = new();
}

public class SyncRenewalPackageItemDto
{
    /// <summary>THREE_MONTHS | SIX_MONTHS | ONE_YEAR | TWO_YEARS | THREE_YEARS | SIX_YEARS</summary>
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Positioning { get; set; }
    public int Months { get; set; }
    public decimal? CustomerPrice { get; set; }
    public int WarrantyMonths { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}