using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.Constants;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class SOSService : ISOSService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ICurrentLocationRepository _currentLocationRepository;
    private readonly IDeviceEventRepository _deviceEventRepository;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly ILogger<SOSService> _logger;

    public SOSService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ICurrentLocationRepository currentLocationRepository,
        IDeviceEventRepository deviceEventRepository,
        IPushNotificationService pushNotificationService,
        ILogger<SOSService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _currentLocationRepository = currentLocationRepository;
        _deviceEventRepository = deviceEventRepository;
        _pushNotificationService = pushNotificationService;
        _logger = logger;
    }

    public async Task<ApiResponse<string>> TriggerSOSAsync(Guid vehicleId)
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
        if (customer is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Profile not found.", "No customer profile exists for this account.");
        }

        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);
        if (vehicle is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Vehicle not found.", $"No vehicle exists with ID '{vehicleId}'.");
        }

        // Deliberately no staff bypass here, unlike most other read-style
        // ownership checks in this API -- SOS is a personal safety trigger
        // specifically for the vehicle's own owner, not something staff
        // should be able to fire on someone else's behalf.
        if (vehicle.CustomerId != customer.CustomerId)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Vehicle not found.", $"No vehicle exists with ID '{vehicleId}'.");
        }

        // Best-effort: an SOS with no location is still far more useful
        // than silently failing because location data happened to be
        // momentarily unavailable. Location fields simply stay null on
        // the Alert if none exists yet.
        var location = await _currentLocationRepository.GetByVehicleAsync(vehicleId);

        var alert = new Alert
        {
            VehicleId = vehicleId,
            DeviceId = location?.DeviceId,
            AlertType = AlertType.SOS,
            Message = "SOS triggered by vehicle owner.",
            Latitude = location?.Latitude,
            Longitude = location?.Longitude,
            TriggeredAt = DateTime.UtcNow,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Alerts.AddAsync(alert);

        // Mirrors the gateway's own DeviceEvents convention exactly
        // (constants/event_types.py: SOS, constants/severity.py:
        // CRITICAL=4) so this row is indistinguishable in format from one
        // the gateway itself would have written, just triggered by the
        // app instead of device hardware.
        //
        // Unlike Alert.DeviceId (nullable), DeviceEvent.DeviceId is
        // required -- if there's no location data yet for this vehicle,
        // there's no device to attribute the event to, so the DeviceEvent
        // is skipped entirely (the Alert still gets created either way;
        // this is purely an additional record, not a replacement).
        if (location is not null)
        {
            var deviceEvent = new DeviceEvent
            {
                DeviceId = location.DeviceId,
                VehicleId = vehicleId,
                EventType = DeviceEventTypes.SOS,
                Severity = DeviceEventSeverity.Critical,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Description = $"SOS triggered by vehicle owner for {vehicle.VehicleNumber}.",
                CreatedAt = DateTime.UtcNow
            };

            await _deviceEventRepository.AddAsync(deviceEvent);
        }
        else
        {
            _logger.LogWarning(
                "SOS triggered for vehicle {VehicleId} with no current location on record -- Alert will still be created, DeviceEvent will be skipped (DeviceId is required and unavailable).",
                vehicleId);
        }

        // Single save covering both writes -- either both the Alert and
        // the DeviceEvent get committed together, or neither does, rather
        // than two separate round-trips that could leave one written and
        // the other missing if something failed in between.
        await _unitOfWork.SaveChangesAsync();

        _logger.LogWarning("SOS triggered for vehicle {VehicleId} ({VehicleNumber}) by customer {CustomerId}",
            vehicleId, vehicle.VehicleNumber, customer.CustomerId);

        // Pushes to every one of the CUSTOMER'S OWN other registered
        // devices (existing SendAlertPushAsync already handles multiple
        // tokens/devices). Does NOT reach emergency contacts or shared
        // viewers -- that depends on Vehicle Sharing, explicitly deferred
        // to a later release, same as payment gateway integration.
        await _pushNotificationService.SendAlertPushAsync(
            customer.CustomerId,
            "SOS Alert",
            $"Emergency alert triggered for {vehicle.VehicleNumber}.");

        return ApiResponse<string>.Ok("OK", "SOS triggered successfully.");
    }
}