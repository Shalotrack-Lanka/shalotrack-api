using ShaloTrack_API.Enums;
using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

public class Subscription
{
    [Key]
    public Guid SubscriptionId { get; set; }

    public Guid CustomerId { get; set; }

    public SubscriptionPlan Plan { get; set; }

    // Determined server-side from Plan, never trusted from the client --
    // an Android app sending its own price would let anyone request a
    // 3-year plan at the Free price by just editing the request.
    public decimal PriceLkr { get; set; }

    public PaymentProvider PaymentProvider { get; set; }
    public SubscriptionStatus Status { get; set; }

    // Null until the subscription actually becomes Active (Free plans
    // activate immediately; paid plans wait for ConfirmPaymentAsync).
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    public Customer Customer { get; set; } = null!;
}