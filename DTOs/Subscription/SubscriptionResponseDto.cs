namespace ShaloTrack_API.DTOs.Subscription;

public class SubscriptionResponseDto
{
    public Guid SubscriptionId { get; set; }
    public string Plan { get; set; } = string.Empty;
    public decimal PriceLkr { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PaymentProvider { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime CreatedAt { get; set; }

    // Only meaningfully populated on the response to a fresh
    // RequestSubscriptionAsync call -- empty on GetMySubscriptionsAsync
    // list results, where there's nothing new to instruct the customer
    // about.
    public string InstructionsMessage { get; set; } = string.Empty;
}