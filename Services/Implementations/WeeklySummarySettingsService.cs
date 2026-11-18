using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.WeeklySummary;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// The signed-in customer's own on/off switch for the weekly summary push. It always acts on the
/// caller's own record (resolved from the token), never on an ID from the request, so there is
/// nothing to probe or tamper with.
/// </summary>
public class WeeklySummarySettingsService : IWeeklySummarySettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public WeeklySummarySettingsService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<WeeklySummarySettingsDto>> GetAsync()
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
            return ApiResponse<WeeklySummarySettingsDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");

        return ApiResponse<WeeklySummarySettingsDto>.Ok(
            new WeeklySummarySettingsDto { Enabled = customer.WeeklySummaryEnabled },
            "Weekly summary setting retrieved successfully.");
    }

    public async Task<ApiResponse<WeeklySummarySettingsDto>> UpdateAsync(WeeklySummarySettingsDto dto)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
            return ApiResponse<WeeklySummarySettingsDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");

        // One UPDATE. The context is NoTracking, so setting the property and calling SaveChanges wrote nothing.
        await _unitOfWork.Customers.SetWeeklySummaryAsync(customer.CustomerId, dto.Enabled, DateTime.UtcNow);

        return ApiResponse<WeeklySummarySettingsDto>.Ok(
            new WeeklySummarySettingsDto { Enabled = dto.Enabled },
            "Weekly summary setting saved.");
    }

    private async Task<Models.Customer?> ResolveCustomerAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid)) return null;
        return await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
    }
}