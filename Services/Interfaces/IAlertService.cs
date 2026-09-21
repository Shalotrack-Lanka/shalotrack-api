using ShaloTrack_API.DTOs.Alert;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IAlertService
{
    Task<ApiResponse<IReadOnlyList<AlertResponseDto>>> GetMyAlertsAsync(int page, int pageSize, Guid? vehicleId = null);
    Task<ApiResponse<string>> MarkAsReadAsync(long alertId);
    Task<ApiResponse<string>> RegisterFcmTokenAsync(RegisterFcmTokenDto dto);

    /// <summary>
    /// NEW -- report generation feature. Requires a specific vehicle and a
    /// bounded [from, to] window, and returns a per-type count summary
    /// alongside the full list.
    /// </summary>
    Task<ApiResponse<AlertReportResponseDto>> GetAlertReportAsync(Guid vehicleId, DateTime from, DateTime to);
}