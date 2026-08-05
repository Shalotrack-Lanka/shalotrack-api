using ShaloTrack_API.Models;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// No real payment gateway integrated yet -- that needs a PayHere (or
/// similar) merchant account, a business/legal step, not a code change.
/// Subscriptions created through this provider are recorded as
/// PendingPayment; staff confirm payment manually (e.g. after a bank
/// transfer) via SubscriptionService.ConfirmPaymentAsync.
///
/// This is a real, complete implementation of how the business actually
/// operates right now, not a placeholder -- but it's deliberately built
/// behind IPaymentProvider so a real gateway can be added later as a new
/// implementation, not a rewrite of everything that currently depends on
/// this interface.
/// </summary>
public class ManualPaymentProvider : IPaymentProvider
{
    public Enums.PaymentProvider ProviderType => Enums.PaymentProvider.Manual;

    public Task<PaymentInitiationResult> InitiatePaymentAsync(Subscription subscription)
    {
        return Task.FromResult(new PaymentInitiationResult
        {
            RequiresRedirect = false,
            InstructionsMessage =
                $"Your {subscription.Plan} plan request (Rs. {subscription.PriceLkr:N0}) has been recorded. " +
                "Our team will contact you to confirm payment and activate your subscription."
        });
    }
}