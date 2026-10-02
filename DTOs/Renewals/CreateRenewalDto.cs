namespace ShaloTrack_API.DTOs.Renewals;

public class CreateRenewalDto
{
    public Guid VehicleId { get; set; }

    /// <summary>ThreeMonths | SixMonths | OneYear | TwoYears | ThreeYears | SixYears</summary>
    public string Duration { get; set; } = string.Empty;

    /// <summary>BankSlip (default). Gateway is reserved and currently refused.</summary>
    public string? PaymentMethod { get; set; }

    public string? PaymentReference { get; set; }
    public string? CustomerNote { get; set; }
}