using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class GpsTrackingService : IGpsTrackingService
{
    private readonly IGpsTrackingRepository _repository;

    public GpsTrackingService(
        IGpsTrackingRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>> GetAsync(
        GpsTrackingFilter filter)
    {
        var tracking = await _repository.GetAsync(filter);

        return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Ok(
            tracking,
            "GPS tracking records retrieved successfully."
        );
    }
}