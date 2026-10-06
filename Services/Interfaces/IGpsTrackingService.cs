using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IGpsTrackingService
{
    Task<ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>> GetAsync(GpsTrackingFilter filter);
    Task<ApiResponse<TripsReportResponseDto>> GetTripsSummaryAsync(Guid vehicleId, DateTime from, DateTime to);

    /// <summary>
    /// Trip report for background jobs (e.g. the weekly summary). Does NOT check who is asking:
    /// the caller must already know the vehicle belongs to the person being notified. Returns
    /// null when the vehicle's subscription has lapsed (no trip data is served for it).
    /// </summary>
    Task<TripsReportResponseDto?> GetTripsReportForSystemAsync(Guid vehicleId, DateTime from, DateTime to);
}