using System.ComponentModel.DataAnnotations;
using ShaloTrack_API.Enums;

namespace ShaloTrack_API.Models;

/// <summary>
/// A customer's request to renew ONE device's subscription. It records the customer's intent and
/// payment proof only. It never changes access: the subscription itself is still written by staff in
/// the admin portal (the single source of truth), which then marks this request approved.
/// </summary>
public class RenewalRequest
{
    [Key]
    public Guid RenewalRequestId { get; set; }

    public Guid CustomerId { get; set; }
    public Guid VehicleId { get; set; }

    /// <summary>The device on the vehicle when the request was made (a later replacement does not rewrite history).</summary>
    public string ImeiNumber { get; set; } = string.Empty;

    public RenewalDuration Duration { get; set; }
    public RenewalPaymentMethod PaymentMethod { get; set; }
    public RenewalStatus Status { get; set; }

    /// <summary>Not priced yet (waiting on the price list); reserved for the gateway and the price table.</summary>
    public decimal? AmountLkr { get; set; }

    /// <summary>Bank transfer reference typed by the customer (optional).</summary>
    public string? PaymentReference { get; set; }
    public string? CustomerNote { get; set; }

    public DateTime? SlipUploadedAt { get; set; }

    /// <summary>SHA-256 of the current slip. Staff must approve exactly the slip they looked at.</summary>
    public string? SlipSha256 { get; set; }

    public string? DecisionReason { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecidedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public RenewalSlip? Slip { get; set; }
}