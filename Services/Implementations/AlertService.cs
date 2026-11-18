using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.Alert;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class AlertService : IAlertService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    private const int MaxReportRangeDays = 90;

    public AlertService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<AlertResponseDto>>> GetMyAlertsAsync(int page, int pageSize, Guid? vehicleId = null)
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

        var alerts = await _unitOfWork.Alerts.GetByCustomerAsync(customer.CustomerId, page, pageSize, vehicleId);

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

        // NoTracking context: setting alert.IsRead and calling SaveChanges wrote nothing (it came back unread
        // after a refresh). One targeted UPDATE instead, scoped to the owner's vehicle.
        await _unitOfWork.Alerts.MarkReadAsync(alertId, alert.Vehicle.CustomerId);

        return ApiResponse<string>.Ok("OK", "Alert marked as read.");
    }

    public async Task<ApiResponse<int>> GetUnreadCountAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<int>.Fail((int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
        if (customer is null)
        {
            return ApiResponse<int>.Fail((int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var count = await _unitOfWork.Alerts.CountUnreadByCustomerAsync(customer.CustomerId);
        return ApiResponse<int>.Ok(count, "Unread alerts counted.");
    }

    public async Task<ApiResponse<int>> MarkAllAsReadAsync(Guid? vehicleId)
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<int>.Fail((int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
        if (customer is null)
        {
            return ApiResponse<int>.Fail((int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        // The UPDATE itself only touches alerts on the caller's own vehicles, so a vehicle id that is not
        // theirs simply changes nothing. Checked anyway so the reply is an honest 404, same as elsewhere.
        if (vehicleId.HasValue)
        {
            var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId.Value);
            if (vehicle is null || vehicle.CustomerId != customer.CustomerId)
            {
                return ApiResponse<int>.Fail((int)HttpStatusCode.NotFound, "Vehicle not found.", $"No vehicle exists with ID '{vehicleId.Value}'.");
            }
        }

        var changed = await _unitOfWork.Alerts.MarkAllReadAsync(customer.CustomerId, vehicleId);
        return ApiResponse<int>.Ok(changed, "Alerts marked as read.");
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
            // SECURITY: an FCM token identifies the app install, not the person. When a second customer
            // signs in on the same phone, the token must move to them. Before this fix the row kept the
            // FIRST customer's id, so the new customer kept receiving the previous customer's vehicle
            // alerts (vehicle number + alert type) and never received their own.
            // One UPDATE: the NoTracking context made the old "existing.CustomerId = ...; SaveChanges" a no-op,
            // so the token never actually moved.
            await _unitOfWork.FcmTokens.ReassignAsync(dto.FcmToken, customer.CustomerId, dto.Platform, DateTime.UtcNow);
            return ApiResponse<string>.Ok("OK", "Token registered.");
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

        return ApiResponse<string>.Ok("OK", "Token registered.");
    }

    public async Task<ApiResponse<string>> UnregisterFcmTokenAsync(RegisterFcmTokenDto dto)
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<string>.Fail((int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
        if (customer is not null)
        {
            // Scoped to the caller: a customer can never delete another customer's token, even by guessing it.
            await _unitOfWork.FcmTokens.DeleteForCustomerAsync(customer.CustomerId, dto.FcmToken);
        }

        // Same reply whether or not the token existed, so this endpoint can't be used to probe tokens.
        return ApiResponse<string>.Ok("OK", "Token removed.");
    }

    public async Task<ApiResponse<AlertReportResponseDto>> GetAlertReportAsync(Guid vehicleId, DateTime from, DateTime to)
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<AlertReportResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        if (to <= from)
        {
            return ApiResponse<AlertReportResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Invalid date range.", "'to' must be after 'from'.");
        }

        if ((to - from).TotalDays > MaxReportRangeDays)
        {
            return ApiResponse<AlertReportResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest,
                "Date range too large.",
                $"Reports are limited to {MaxReportRangeDays} days per request. " +
                $"Split longer ranges into multiple calls.");
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
        if (customer is null)
        {
            return ApiResponse<AlertReportResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var vehicle = await _unitOfWork.Vehicles.GetByIdForOwnershipCheckAsync(vehicleId);
        if (vehicle is null)
        {
            return ApiResponse<AlertReportResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Vehicle not found.", $"No vehicle exists with ID '{vehicleId}'.");
        }

        bool isOwner = vehicle.CustomerId == customer.CustomerId;
        bool hasAcceptedShare = false;
        if (!_currentUser.IsStaff && !isOwner)
        {
            var share = await _unitOfWork.VehicleShares.GetByVehicleAndSharedWithAsync(vehicleId, customer.CustomerId);
            hasAcceptedShare = share is not null && share.Status == VehicleShareStatus.Accepted;
        }

        if (!_currentUser.IsStaff && !isOwner && !hasAcceptedShare && !vehicle.IsDemoVehicle)
        {
            return ApiResponse<AlertReportResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Vehicle not found.", $"No vehicle exists with ID '{vehicleId}'.");
        }

        var alerts = await _unitOfWork.Alerts.GetByVehicleAndDateRangeAsync(vehicleId, from, to);

        var countsByType = alerts
            .GroupBy(a => a.AlertType.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var report = new AlertReportResponseDto
        {
            VehicleId = vehicleId,
            From = from,
            To = to,
            TotalCount = alerts.Count,
            CountsByType = countsByType,
            Alerts = alerts.Select(ToDto).ToList()
        };

        return ApiResponse<AlertReportResponseDto>.Ok(report, "Alert report retrieved successfully.");
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