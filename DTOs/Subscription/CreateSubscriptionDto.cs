namespace ShaloTrack_API.DTOs.Subscription;

public class CreateSubscriptionDto
{
    // "Free" | "OneYear" | "TwoYears" | "ThreeYears" -- matches
    // SubscriptionPlan enum names exactly, parsed in the service.
    public string Plan { get; set; } = string.Empty;
}