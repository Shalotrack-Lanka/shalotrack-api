using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class GpsTrackingService : IGpsTrackingService
{
    private readonly IGpsTrackingRepository _repository;
    private readonly IUnitOfWork _unitOfWork;      // NEW — to resolve the vehicle's owner
    private readonly ICurrentUser _currentUser;    // NEW

    public GpsTrackingService(
        IGpsTrackingRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>> GetAsync(
        GpsTrackingFilter filter)
    {
        // FIX: VehicleId was optional. Omitting it returned every vehicle's trail in
        // the system — a full-fleet location-history leak. It's now required.
        if (!filter.VehicleId.HasValue)
        {
            return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Fail(
                (int)HttpStatusCode.BadRequest,
                "VehicleId is required.",
                "A specific vehicleId must be provided to retrieve tracking history."
            );
        }

        // FIX: no ownership check existed. Any authenticated user could pass any
        // vehicleId and read that vehicle's full movement trail.
        if (!_currentUser.IsStaff)
        {
            var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(filter.VehicleId.Value);
            if (vehicle is null ||
                !string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal))
            {
                // 404, not 403 — don't confirm the vehicle exists to a non-owner.
                return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Fail(
                    (int)HttpStatusCode.NotFound,
                    "Vehicle not found.",
                    $"No vehicle exists with ID '{filter.VehicleId.Value}'."
                );
            }
        }

        // Cap the page size server-side so a client can't request an unbounded trail.
        if (filter.PageSize > 500) filter.PageSize = 500;

        var tracking = await _repository.GetAsync(filter);

        return ApiResponse<IReadOnlyList<GpsTrackingResponseDto>>.Ok(
            tracking,
            "GPS tracking records retrieved successfully."
        );
    }
}