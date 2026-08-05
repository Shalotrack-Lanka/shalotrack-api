using ShaloTrack_API.Models;

namespace ShaloTrack_API.Services.Interfaces;

/// <summary>
/// Abstraction over how a subscription actually gets paid for. Only one
/// implementation exists today (ManualPaymentProvider), since this
/// project has no payment gateway merchant account yet. Adding a real
/// gateway (PayHere, etc.) later means implementing this interface and
/// registering it in BusinessServiceExtensions -- SubscriptionService and
/// everything else that depends on IPaymentProvider does not change.
/// </summary>
public interface IPaymentProvider
{
    Enums.PaymentProvider ProviderType { get; }

    /// <summary>
    /// Kicks off payment for a subscription. For a real gateway this would
    /// typically return a checkout URL to redirect the customer to; for
    /// the manual provider it returns instructions describing how to pay
    /// and that confirmation is manual.
    /// </summary>
    Task<PaymentInitiationResult> InitiatePaymentAsync(Subscription subscription);
}

public class PaymentInitiationResult
{
    public bool RequiresRedirect { get; set; }
    public string? RedirectUrl { get; set; }
    public string InstructionsMessage { get; set; } = string.Empty;
}