using ShaloTrack_API.DTOs.Subscription;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface ISubscriptionService
{
    Task<ApiResponse<SubscriptionResponseDto>> RequestSubscriptionAsync(CreateSubscriptionDto dto);
    Task<ApiResponse<IReadOnlyList<SubscriptionResponseDto>>> GetMySubscriptionsAsync();

    /// <summary>Staff-only: marks a PendingPayment subscription Active
    /// after payment has been confirmed manually (e.g. bank transfer
    /// checked against a bank statement).</summary>
    Task<ApiResponse<string>> ConfirmPaymentAsync(Guid subscriptionId);
}