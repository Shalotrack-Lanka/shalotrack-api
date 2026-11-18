using System.Net;
using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Auth;
using ShaloTrack_API.Data;
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
    private readonly ShaloTrackDbContext _db;

    public SubscriptionService(IUnitOfWork unitOfWork, ICurrentUser currentUser, IPaymentProvider paymentProvider, ShaloTrackDbContext db)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _paymentProvider = paymentProvider;
        _db = db;
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
        //
        // FIX (18 Nov 2026): a Free plan is Active with no end date, so under the old rule it blocked every
        // paid request forever and no Free customer could ever upgrade. An Active Free plan now only blocks
        // asking for Free again; it is cancelled when the paid plan's payment is confirmed.
        var existing = await _unitOfWork.Subscriptions.GetByCustomerAsync(customer.CustomerId);
        var blockingSubscription = existing.FirstOrDefault(s =>
            s.Status == SubscriptionStatus.PendingPayment
            || (s.Status == SubscriptionStatus.Active && (s.Plan != SubscriptionPlan.Free || plan == SubscriptionPlan.Free)));
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
        var (price, priceError) = await ResolvePriceAsync(plan);
        if (priceError is not null)
        {
            return ApiResponse<SubscriptionResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "This plan is not available.", priceError);
        }

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

        // One conditional UPDATE. The context is NoTracking, so the old "set the fields; SaveChanges" wrote
        // nothing: the plan stayed PendingPayment while the Free plan below WAS cancelled, leaving the customer
        // with no active plan. The WHERE also stops a double confirm from re-dating an already active plan.
        var now = DateTime.UtcNow;
        var endDate = now.Add(GetDurationForPlan(subscription.Plan));
        var activated = await _db.Subscriptions
            .Where(s => s.SubscriptionId == subscriptionId && s.Status == SubscriptionStatus.PendingPayment)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, SubscriptionStatus.Active)
                .SetProperty(s => s.StartDate, (DateTime?)now)
                .SetProperty(s => s.EndDate, (DateTime?)endDate)
                .SetProperty(s => s.ConfirmedAt, (DateTime?)now));
        if (activated != 1)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.Conflict, "Not pending payment.", "This subscription was confirmed or changed meanwhile.");
        }

        // The paid plan replaces the customer's Free plan (one UPDATE; nothing to load). Only after activation
        // succeeded, so a customer is never left without a plan.
        if (subscription.Plan != SubscriptionPlan.Free)
        {
            await _db.Subscriptions
                .Where(s => s.CustomerId == subscription.CustomerId
                    && s.SubscriptionId != subscription.SubscriptionId
                    && s.Plan == SubscriptionPlan.Free
                    && s.Status == SubscriptionStatus.Active)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Status, SubscriptionStatus.Cancelled));
        }

        return ApiResponse<string>.Ok("OK", "Payment confirmed, subscription activated.");
    }

    /// <summary>
    /// The price of an app plan comes from the same price list the admin portal pushes for device
    /// renewals (ONE_YEAR / TWO_YEARS / THREE_YEARS), so the two can never disagree. If the admin has
    /// switched a package off or removed its price, the plan cannot be ordered. Only when no price list
    /// has ever been synced do the Renewal Plans guideline's prices (v1.0, 30 Sep 2026) apply.
    /// </summary>
    private async Task<(decimal Price, string? Error)> ResolvePriceAsync(SubscriptionPlan plan)
    {
        if (plan == SubscriptionPlan.Free) return (0m, null);

        var code = plan switch
        {
            SubscriptionPlan.OneYear => "ONE_YEAR",
            SubscriptionPlan.TwoYears => "TWO_YEARS",
            SubscriptionPlan.ThreeYears => "THREE_YEARS",
            _ => throw new ArgumentOutOfRangeException(nameof(plan), plan, "Unhandled subscription plan.")
        };

        var package = await _db.RenewalPackages.AsNoTracking().FirstOrDefaultAsync(p => p.Code == code);
        if (package is not null)
        {
            if (package.IsOffered) return (package.CustomerPriceLkr!.Value, null);
            return (0m, "This plan is not on offer right now. Choose another one or contact support.");
        }

        if (await _db.RenewalPackages.AsNoTracking().AnyAsync())
        {
            return (0m, "This plan is not on offer right now. Choose another one or contact support.");
        }

        return (GuidelinePriceFor(plan), null);
    }

    /// <summary>Renewal Plans guideline v1.0 prices; used only until the first price list arrives from the admin portal.</summary>
    private static decimal GuidelinePriceFor(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.OneYear => 2999m,
        SubscriptionPlan.TwoYears => 4999m,
        SubscriptionPlan.ThreeYears => 6999m,
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