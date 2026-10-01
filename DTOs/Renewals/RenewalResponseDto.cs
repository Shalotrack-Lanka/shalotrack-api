namespace ShaloTrack_API.DTOs.Renewals;

public class RenewalResponseDto
{
    public Guid RenewalRequestId { get; set; }
    public Guid VehicleId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool HasSlip { get; set; }
    public string? PaymentReference { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? InstructionsMessage { get; set; }
}