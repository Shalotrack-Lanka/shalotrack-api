using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.DeviceEvent;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;
using System.Net;

namespace ShaloTrack_API.Services.Implementations;

public class DeviceEventService : IDeviceEventService
{
    private readonly IDeviceEventRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public DeviceEventService(
        IDeviceEventRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    // FIX: real, currently-live security gap found during a systematic
    // audit -- this endpoint had zero authorization role restriction and
    // this method had zero ownership validation at all, despite the
    // controller's own comment explicitly flagging the risk ("a customer
    // could filter to another's device") without it ever actually being
    // addressed. Non-staff callers must specify VehicleId and must own
    // it -- deny-by-default rather than allow an unscoped, system-wide
    // query across every customer's device events.
    //
    // PERFORMANCE FIX: switched from GetByIdAsync (loads full DeviceAssignment
    // history + Device records) to GetByIdForOwnershipCheckAsync (loads Customer
    // only). Ownership checks here only need Customer.FirebaseUid.
    public async Task<ApiResponse<List<DeviceEventResponseDto>>> GetAsync(
        DeviceEventFilter filter)
    {
        if (!_currentUser.IsStaff)
        {
            if (!filter.VehicleId.HasValue)
            {
                return ApiResponse<List<DeviceEventResponseDto>>.Fail(
                    (int)HttpStatusCode.BadRequest,
                    "VehicleId is required.",
                    "A specific vehicle must be specified.");
            }

            var vehicle = await _unitOfWork.Vehicles.GetByIdForOwnershipCheckAsync(filter.VehicleId.Value);
            bool isOwner = vehicle is not null &&
                string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal);
            // NEW -- the one, shared demo vehicle, readable by every customer.
            bool isDemoVehicle = vehicle?.IsDemoVehicle ?? false;
            if (!isOwner && !isDemoVehicle)
            {
                return ApiResponse<List<DeviceEventResponseDto>>.Fail(
                    (int)HttpStatusCode.NotFound,
                    "Vehicle not found.",
                    $"No vehicle exists with ID '{filter.VehicleId.Value}'.");
            }
        }

        var deviceevents = await _repository.GetAsync(filter);

        return ApiResponse<List<DeviceEventResponseDto>>.Ok(deviceevents);
    }

    public async Task<ApiResponse<DeviceEventResponseDto>> GetByIdAsync(
        long eventId)
    {
        var deviceEvent = await _repository.GetByIdAsync(eventId);

        if (deviceEvent == null)
        {
            return ApiResponse<DeviceEventResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Device event not found.");
        }

        // FIX: same real gap as GetAsync -- was returning any event by ID
        // with zero ownership check. An eventId is a plain auto-
        // incrementing long, trivially enumerable.
        //
        // PERFORMANCE FIX: GetByIdForOwnershipCheckAsync instead of
        // GetByIdAsync — only Customer.FirebaseUid needed here.
        if (!_currentUser.IsStaff)
        {
            bool isOwner = false;
            bool isDemoVehicle = false;
            if (deviceEvent.VehicleId.HasValue)
            {
                var vehicle = await _unitOfWork.Vehicles.GetByIdForOwnershipCheckAsync(deviceEvent.VehicleId.Value);
                isOwner = vehicle is not null &&
                    string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal);
                // NEW -- the one, shared demo vehicle, readable by every customer.
                isDemoVehicle = vehicle?.IsDemoVehicle ?? false;
            }
            // No VehicleId at all means no ownership is possible to prove
            // -- deny for non-staff rather than allow it through.
            if (!isOwner && !isDemoVehicle)
            {
                return ApiResponse<DeviceEventResponseDto>.Fail(
                    (int)HttpStatusCode.NotFound,
                    "Device event not found.");
            }
        }

        return ApiResponse<DeviceEventResponseDto>.Ok(deviceEvent);
    }
}