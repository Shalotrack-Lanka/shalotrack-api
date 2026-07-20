using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.Alert;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class AlertService : IAlertService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public AlertService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<AlertResponseDto>>> GetMyAlertsAsync(int page, int pageSize)
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<IReadOnlyList<AlertResponseDto>>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
        if (customer is null)
        {
            return ApiResponse<IReadOnlyList<AlertResponseDto>>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        if (pageSize > 100) pageSize = 100;
        if (page < 1) page = 1;

        var alerts = await _unitOfWork.Alerts.GetByCustomerAsync(customer.CustomerId, page, pageSize);

        var dtoList = alerts.Select(ToDto).ToList();

        return ApiResponse<IReadOnlyList<AlertResponseDto>>.Ok(dtoList, "Alerts retrieved successfully.");
    }

    public async Task<ApiResponse<string>> MarkAsReadAsync(long alertId)
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<string>.Fail((int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var alert = await _unitOfWork.Alerts.GetByIdAsync(alertId);
        if (alert is null || !string.Equals(alert.Vehicle.Customer.FirebaseUid, uid, StringComparison.Ordinal))
        {
            return ApiResponse<string>.Fail((int)HttpStatusCode.NotFound, "Alert not found.", $"No alert exists with ID '{alertId}'.");
        }

        alert.IsRead = true;
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<string>.Ok("OK", "Alert marked as read.");
    }

    public async Task<ApiResponse<string>> RegisterFcmTokenAsync(RegisterFcmTokenDto dto)
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<string>.Fail((int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
        if (customer is null)
        {
            return ApiResponse<string>.Fail((int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var existing = await _unitOfWork.FcmTokens.GetByTokenAsync(dto.FcmToken);
        if (existing is not null)
        {
            existing.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return ApiResponse<string>.Ok("OK", "Token already registered, refreshed.");
        }

        var token = new CustomerFcmToken
        {
            CustomerId = customer.CustomerId,
            FcmToken = dto.FcmToken,
            Platform = dto.Platform,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.FcmTokens.AddAsync(token);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<string>.Ok("OK", "Token registered successfully.");
    }

    private static AlertResponseDto ToDto(Alert alert)
    {
        return new AlertResponseDto
        {
            AlertId = alert.AlertId,
            VehicleId = alert.VehicleId,
            VehicleNumber = alert.Vehicle.VehicleNumber,
            AlertType = alert.AlertType.ToString(),
            Message = alert.Message,
            Latitude = alert.Latitude,
            Longitude = alert.Longitude,
            TriggeredAt = alert.TriggeredAt,
            IsRead = alert.IsRead
        };
    }
}