using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IGpsTrackingService
{
    Task<ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>> GetAsync(GpsTrackingFilter filter);
    Task<ApiResponse<TripsReportResponseDto>> GetTripsSummaryAsync(Guid vehicleId, DateTime from, DateTime to);
}