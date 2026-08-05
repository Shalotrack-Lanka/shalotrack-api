using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.Subscription;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IPaymentProvider _paymentProvider;

    public SubscriptionService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IPaymentProvider paymentProvider)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _paymentProvider = paymentProvider;
    }

    public async Task<ApiResponse<SubscriptionResponseDto>> RequestSubscriptionAsync(CreateSubscriptionDto dto)
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<SubscriptionResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
        if (customer is null)
        {
            return ApiResponse<SubscriptionResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        if (!Enum.TryParse<SubscriptionPlan>(dto.Plan, ignoreCase: true, out var plan))
        {
            return ApiResponse<SubscriptionResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Invalid plan.",
                $"'{dto.Plan}' is not a recognized plan. Expected Free, OneYear, TwoYears, or ThreeYears.");
        }

        // Subscriptions here map to a single device warranty period, not a
        // feature tier -- there is no meaningful state where a customer
        // has two simultaneous "active periods". Block a new request
        // while one is already Active or PendingPayment; Expired/
        // Cancelled subscriptions don't block (a lapsed customer should
        // be able to renew).
        var existing = await _unitOfWork.Subscriptions.GetByCustomerAsync(customer.CustomerId);
        var blockingSubscription = existing.FirstOrDefault(s =>
            s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PendingPayment);
        if (blockingSubscription is not null)
        {
            return ApiResponse<SubscriptionResponseDto>.Fail(
                (int)HttpStatusCode.Conflict,
                "You already have a subscription in progress.",
                $"Your {blockingSubscription.Plan} plan is currently '{blockingSubscription.Status}'. " +
                "Wait for it to expire, or contact support if you believe this is an error.");
        }

        // Price is determined here, server-side, from the plan -- never
        // trusted from the client. A request that sent its own price
        // would let anyone claim a 3-year plan at the Free price by just
        // editing the request body.
        var price = GetPriceForPlan(plan);

        var subscription = new Subscription
        {
            SubscriptionId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            Plan = plan,
            PriceLkr = price,
            CreatedAt = DateTime.UtcNow
        };

        string instructionsMessage;

        if (plan == SubscriptionPlan.Free)
        {
            // Free needs no payment -- there's nothing to confirm, so it
            // activates immediately rather than going through
            // IPaymentProvider at all. No expiry is set (EndDate stays
            // null); the requirement doc's "warranty valid only during
            // active subscription period" note doesn't define a Free-tier
            // duration, so none is invented here.
            subscription.PaymentProvider = PaymentProvider.Manual;
            subscription.Status = SubscriptionStatus.Active;
            subscription.StartDate = DateTime.UtcNow;
            subscription.EndDate = null;
            subscription.ConfirmedAt = DateTime.UtcNow;
            instructionsMessage = "Your Free plan is now active.";
        }
        else
        {
            subscription.PaymentProvider = _paymentProvider.ProviderType;
            subscription.Status = SubscriptionStatus.PendingPayment;

            var paymentResult = await _paymentProvider.InitiatePaymentAsync(subscription);
            instructionsMessage = paymentResult.InstructionsMessage;
        }

        await _unitOfWork.Subscriptions.AddAsync(subscription);
        await _unitOfWork.SaveChangesAsync();

        var responseDto = ToDto(subscription);
        responseDto.InstructionsMessage = instructionsMessage;

        return ApiResponse<SubscriptionResponseDto>.Ok(responseDto, "Subscription request recorded.");
    }

    public async Task<ApiResponse<IReadOnlyList<SubscriptionResponseDto>>> GetMySubscriptionsAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<IReadOnlyList<SubscriptionResponseDto>>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
        if (customer is null)
        {
            return ApiResponse<IReadOnlyList<SubscriptionResponseDto>>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var subscriptions = await _unitOfWork.Subscriptions.GetByCustomerAsync(customer.CustomerId);
        var dtoList = subscriptions.Select(ToDto).ToList();

        return ApiResponse<IReadOnlyList<SubscriptionResponseDto>>.Ok(dtoList, "Subscriptions retrieved successfully.");
    }

    public async Task<ApiResponse<string>> ConfirmPaymentAsync(Guid subscriptionId)
    {
        if (!_currentUser.IsStaff)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.Forbidden, "Not authorized.", "Only staff can confirm subscription payments.");
        }

        var subscription = await _unitOfWork.Subscriptions.GetByIdAsync(subscriptionId);
        if (subscription is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Subscription not found.", $"No subscription exists with ID '{subscriptionId}'.");
        }

        if (subscription.Status != SubscriptionStatus.PendingPayment)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.BadRequest, "Not pending payment.",
                $"This subscription is already '{subscription.Status}', not PendingPayment.");
        }

        subscription.Status = SubscriptionStatus.Active;
        subscription.StartDate = DateTime.UtcNow;
        subscription.EndDate = DateTime.UtcNow.Add(GetDurationForPlan(subscription.Plan));
        subscription.ConfirmedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<string>.Ok("OK", "Payment confirmed, subscription activated.");
    }

    private static decimal GetPriceForPlan(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Free => 0m,
        SubscriptionPlan.OneYear => 2999m,
        SubscriptionPlan.TwoYears => 5499m,
        SubscriptionPlan.ThreeYears => 7999m,
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, "Unhandled subscription plan.")
    };

    private static TimeSpan GetDurationForPlan(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.OneYear => TimeSpan.FromDays(365),
        SubscriptionPlan.TwoYears => TimeSpan.FromDays(730),
        SubscriptionPlan.ThreeYears => TimeSpan.FromDays(1095),
        _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, "Free plans do not go through duration-based confirmation.")
    };

    private static SubscriptionResponseDto ToDto(Subscription subscription)
    {
        return new SubscriptionResponseDto
        {
            SubscriptionId = subscription.SubscriptionId,
            Plan = subscription.Plan.ToString(),
            PriceLkr = subscription.PriceLkr,
            Status = subscription.Status.ToString(),
            PaymentProvider = subscription.PaymentProvider.ToString(),
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            CreatedAt = subscription.CreatedAt
        };
    }
}