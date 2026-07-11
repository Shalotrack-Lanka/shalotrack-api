using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Auth;

/// <summary>
/// Enforces that the caller OWNS the customer named in the route ({customerId}).
/// Use on {customerId} routes only. Resource-id routes (vehicleId/deviceId) are
/// checked in the service layer instead.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class OwnsCustomerAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly string _routeKey;

    public OwnsCustomerAttribute(string routeKey = "customerId") => _routeKey = routeKey;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUser>();
        var uow = context.HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();

        var uid = currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        if (currentUser.IsStaff) return;

        if (!context.RouteData.Values.TryGetValue(_routeKey, out var raw) ||
            !Guid.TryParse(raw?.ToString(), out var customerId))
        {
            context.Result = new BadRequestObjectResult($"Missing or invalid '{_routeKey}'.");
            return;
        }

        var customer = await uow.Customers.GetByIdAsync(customerId);

        if (customer is null ||
            !string.Equals(customer.FirebaseUid, uid, StringComparison.Ordinal))
        {
            context.Result = new NotFoundResult();   // 404, not 403
            return;
        }
    }
}