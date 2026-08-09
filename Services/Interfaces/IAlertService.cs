using ShaloTrack_API.DTOs.Alert;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IAlertService
{
    // NEW: vehicleId optional, null = all vehicles (existing behavior unchanged).
    Task<ApiResponse<IReadOnlyList<AlertResponseDto>>> GetMyAlertsAsync(int page, int pageSize, Guid? vehicleId = null);
    Task<ApiResponse<string>> MarkAsReadAsync(long alertId);
    Task<ApiResponse<string>> RegisterFcmTokenAsync(RegisterFcmTokenDto dto);
}