namespace ShaloTrack_API.DTOs.Renewals;

/// <summary>One package the customer can buy. Customer-safe: no margins, ever.</summary>
public class RenewalPackageDto
{
    /// <summary>Send this as CreateRenewalDto.Duration (ThreeMonths | SixMonths | OneYear | TwoYears | ThreeYears | SixYears).</summary>
    public string Duration { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Positioning { get; set; }
    public int Months { get; set; }
    public decimal PriceLkr { get; set; }
    public int WarrantyMonths { get; set; }
}