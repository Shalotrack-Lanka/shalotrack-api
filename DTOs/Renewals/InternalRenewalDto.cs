namespace ShaloTrack_API.DTOs.Renewals;

/// <summary>What the admin portal sees. Staff-only; never returned to customers.</summary>
public class InternalRenewalDto
{
    public Guid RenewalRequestId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    /// <summary>The admin portal's subscription_model string ("3 Months", "1 Year", ...).</summary>
    public string DurationModel { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string ImeiNumber { get; set; } = string.Empty;
    public Guid VehicleId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? PaymentReference { get; set; }
    public string? CustomerNote { get; set; }
    public bool HasSlip { get; set; }
    public string? SlipSha256 { get; set; }
    public DateTime? SlipUploadedAt { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecidedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}