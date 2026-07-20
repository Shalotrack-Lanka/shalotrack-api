using ShaloTrack_API.DTOs.Alert;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IAlertService
{
    Task<ApiResponse<IReadOnlyList<AlertResponseDto>>> GetMyAlertsAsync(int page, int pageSize);
    Task<ApiResponse<string>> MarkAsReadAsync(long alertId);
    Task<ApiResponse<string>> RegisterFcmTokenAsync(RegisterFcmTokenDto dto);
}