using ShaloTrack_API.DTOs.Alert;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IAlertService
{
    Task<ApiResponse<IReadOnlyList<AlertResponseDto>>> GetMyAlertsAsync(int page, int pageSize, Guid? vehicleId = null);
    Task<ApiResponse<string>> MarkAsReadAsync(long alertId);

    /// <summary>Unread alerts on the caller's own vehicles (app badge). Replaces reading a page of alerts to count them.</summary>
    Task<ApiResponse<int>> GetUnreadCountAsync();

    /// <summary>Marks all the caller's unread alerts as read, or only one vehicle's (which the caller must own).</summary>
    Task<ApiResponse<int>> MarkAllAsReadAsync(Guid? vehicleId);
    Task<ApiResponse<string>> RegisterFcmTokenAsync(RegisterFcmTokenDto dto);

    /// <summary>
    /// Sign-out: stops pushes to this phone for the signed-in customer. Only deletes the token if it
    /// belongs to the caller, and answers the same whether or not it existed.
    /// </summary>
    Task<ApiResponse<string>> UnregisterFcmTokenAsync(RegisterFcmTokenDto dto);

    /// <summary>
    /// NEW -- report generation feature. Requires a specific vehicle and a
    /// bounded [from, to] window, and returns a per-type count summary
    /// alongside the full list.
    /// </summary>
    Task<ApiResponse<AlertReportResponseDto>> GetAlertReportAsync(Guid vehicleId, DateTime from, DateTime to);
}