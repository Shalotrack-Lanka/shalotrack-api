using System.Net;
using System.Text.RegularExpressions;
using ShaloTrack_API.DTOs.Internal;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class DeviceReplacementService : IDeviceReplacementService
{
    private static readonly Regex ImeiPattern = new(@"^\d{15}$", RegexOptions.Compiled);

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentLocationRepository _currentLocations;
    private readonly ILogger<DeviceReplacementService> _logger;

    public DeviceReplacementService(
        IUnitOfWork unitOfWork,
        ICurrentLocationRepository currentLocations,
        ILogger<DeviceReplacementService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentLocations = currentLocations;
        _logger = logger;
    }

    public async Task<ApiResponse<string>> ReplaceAsync(DeviceReplacementSyncDto dto)
    {
        var oldImei = dto.OldImei?.Trim() ?? string.Empty;
        var newImei = dto.NewImei?.Trim() ?? string.Empty;

        if (!ImeiPattern.IsMatch(oldImei) || !ImeiPattern.IsMatch(newImei))
            return ApiResponse<string>.Fail((int)HttpStatusCode.BadRequest, "Invalid IMEI.",
                "Both oldImei and newImei must be 15-digit IMEIs.");

        if (oldImei == newImei)
            return ApiResponse<string>.Fail((int)HttpStatusCode.BadRequest, "Invalid request.",
                "oldImei and newImei must be different.");

        // The new device must already be synced as Activated (the admin pushes it first).
        var newSetup = await _unitOfWork.SetupShalotrackDevices.GetByImeiAsync(newImei);
        if (newSetup is null || newSetup.Status != "Activated")
            return ApiResponse<string>.Fail((int)HttpStatusCode.Conflict, "New device is not activated.",
                "The new device is unknown or not Activated in the device registry.");

        var oldDevice = await _unitOfWork.GpsDevices.GetByImeiAsync(oldImei);
        var oldActive = oldDevice is null
            ? new List<DeviceAssignment>()
            : await _unitOfWork.DeviceAssignments.GetByDeviceAsync(oldDevice.DeviceId, true);

        if (oldActive.Count == 0)
        {
            // Nothing to move: the old device never connected, was never assigned, or this call
            // is a retry that already succeeded.
            return ApiResponse<string>.Ok("No active assignment to move.", "No active assignment to move.");
        }

        // The new device may not have connected yet, so it may have no GpsDevices row.
        // Same defaults the gateway uses when it auto-registers an Activated device.
        var newDevice = await _unitOfWork.GpsDevices.GetByImeiAsync(newImei);
        var createNewDevice = newDevice is null;
        if (newDevice is null)
        {
            newDevice = new GpsDevice
            {
                DeviceId = Guid.NewGuid(),
                ImeiNumber = newImei,
                SimNumber = newSetup.SimNumber,
                DeviceModel = string.IsNullOrWhiteSpace(newSetup.DeviceCategory) ? "V5" : newSetup.DeviceCategory,
                ProtocolType = "GT06",
                ActivationStatus = ActivationStatus.Active,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
        }

        var newActive = createNewDevice
            ? new List<DeviceAssignment>()
            : await _unitOfWork.DeviceAssignments.GetByDeviceAsync(newDevice.DeviceId, true);

        var vehicleIds = oldActive.Select(a => a.VehicleId).Distinct().ToList();

        // Never steal a device that is already on a different vehicle.
        if (newActive.Any(a => !vehicleIds.Contains(a.VehicleId)))
            return ApiResponse<string>.Fail((int)HttpStatusCode.Conflict, "New device already assigned.",
                "The new device is already assigned to a different vehicle.");

        var now = DateTime.UtcNow;
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            if (createNewDevice)
                await _unitOfWork.GpsDevices.AddAsync(newDevice);

            foreach (var assignment in oldActive)
            {
                // Loaded with navigations; drop them so Update() touches only this row and cannot
                // clash with the device instance already loaded above.
                assignment.Vehicle = null!;
                assignment.Device = null!;
                assignment.Status = AssignmentStatus.Removed;
                assignment.RemovedAt = now;
                _unitOfWork.DeviceAssignments.Update(assignment);
            }

            foreach (var vehicleId in vehicleIds.Where(v => newActive.All(a => a.VehicleId != v)))
            {
                await _unitOfWork.DeviceAssignments.AddAsync(new DeviceAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    VehicleId = vehicleId,
                    DeviceId = newDevice.DeviceId,
                    AssignedAt = now,
                    Status = AssignmentStatus.Active,
                });
            }

            // CurrentLocations allows one row per vehicle. The old device's row still points at the
            // vehicle, so the new device's first position would collide with it. Its last-known
            // position is stale anyway; the tracking history keeps the real data.
            await _currentLocations.RemoveByDeviceAsync(oldDevice!.DeviceId);

            newDevice.InstalledAt = now;
            newDevice.UpdatedAt = now;
            if (!createNewDevice) _unitOfWork.GpsDevices.Update(newDevice);

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            _logger.LogError(ex, "Device replacement {Old} -> {New} failed", oldImei, newImei);
            throw;
        }

        _logger.LogInformation("Device replacement {Old} -> {New}: moved {Count} vehicle(s)",
            oldImei, newImei, vehicleIds.Count);
        return ApiResponse<string>.Ok("Vehicle moved to the new device.", "Vehicle moved to the new device.");
    }
}