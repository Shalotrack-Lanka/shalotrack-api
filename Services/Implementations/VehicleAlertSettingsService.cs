using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.Constants;
using ShaloTrack_API.DTOs.VehicleAlertSettings;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Owner-only read/write of a vehicle's speed limit and idle-alert threshold. Anything that is
/// not the caller's own, active, non-demo vehicle answers 404 (never 403), so IDs cannot be
/// probed. Shared viewers receive the alerts but cannot change the thresholds.
/// </summary>
public class VehicleAlertSettingsService : IVehicleAlertSettingsService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IVehicleAlertSettingsProvider _provider;

    public VehicleAlertSettingsService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IVehicleAlertSettingsProvider provider)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _provider = provider;
    }

    public async Task<ApiResponse<VehicleAlertSettingsResponseDto>> GetAsync(Guid vehicleId)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
            return ApiResponse<VehicleAlertSettingsResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");

        // Read-only, so the lightweight no-tracking lookup is enough.
        var vehicle = await _unitOfWork.Vehicles.GetByIdForOwnershipCheckAsync(vehicleId);
        if (!Owns(customer, vehicle))
            return NotFound();

        return ApiResponse<VehicleAlertSettingsResponseDto>.Ok(ToDto(vehicle!), "Alert settings retrieved successfully.");
    }

    public async Task<ApiResponse<VehicleAlertSettingsResponseDto>> UpdateAsync(Guid vehicleId, UpdateVehicleAlertSettingsDto dto)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
            return ApiResponse<VehicleAlertSettingsResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");

        if (dto.SpeedLimitKmh < AlertDefaults.MinSpeedLimitKmh || dto.SpeedLimitKmh > AlertDefaults.MaxSpeedLimitKmh)
            return ApiResponse<VehicleAlertSettingsResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Invalid speed limit.",
                $"Speed limit must be between {AlertDefaults.MinSpeedLimitKmh} and {AlertDefaults.MaxSpeedLimitKmh} km/h.");

        if (dto.IdleAlertEnabled &&
            (dto.IdleAlertMinutes < AlertDefaults.MinIdleMinutes || dto.IdleAlertMinutes > AlertDefaults.MaxIdleMinutes))
            return ApiResponse<VehicleAlertSettingsResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Invalid idle time.",
                $"Idle time must be between {AlertDefaults.MinIdleMinutes} and {AlertDefaults.MaxIdleMinutes} minutes.");

        // Tracked entity. Deliberately NOT calling Vehicles.Update(): that marks the whole loaded
        // graph (customer, assignments, device) as modified. With plain change tracking EF writes
        // only the three columns changed below.
        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);
        if (!Owns(customer, vehicle))
            return NotFound();

        vehicle!.SpeedLimitKmh = dto.SpeedLimitKmh == AlertDefaults.SpeedLimitKmh ? null : dto.SpeedLimitKmh;
        vehicle.IdleAlertMinutes = dto.IdleAlertEnabled ? dto.IdleAlertMinutes : null;
        vehicle.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync();
        _provider.Invalidate(vehicleId);

        return ApiResponse<VehicleAlertSettingsResponseDto>.Ok(ToDto(vehicle), "Alert settings saved.");
    }

    private static bool Owns(Customer customer, Vehicle? vehicle) =>
        vehicle is not null
        && vehicle.IsActive
        && !vehicle.IsDemoVehicle
        && vehicle.CustomerId == customer.CustomerId;

    private static ApiResponse<VehicleAlertSettingsResponseDto> NotFound() =>
        ApiResponse<VehicleAlertSettingsResponseDto>.Fail(
            (int)HttpStatusCode.NotFound, "Vehicle not found.", "No such vehicle.");

    private async Task<Customer?> ResolveCustomerAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid)) return null;
        return await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
    }

    private static VehicleAlertSettingsResponseDto ToDto(Vehicle v) => new()
    {
        SpeedLimitKmh = v.SpeedLimitKmh ?? AlertDefaults.SpeedLimitKmh,
        IdleAlertEnabled = v.IdleAlertMinutes.HasValue,
        IdleAlertMinutes = v.IdleAlertMinutes ?? AlertDefaults.IdleMinutes,
        DefaultSpeedLimitKmh = AlertDefaults.SpeedLimitKmh,
        MinSpeedLimitKmh = AlertDefaults.MinSpeedLimitKmh,
        MaxSpeedLimitKmh = AlertDefaults.MaxSpeedLimitKmh,
        MinIdleMinutes = AlertDefaults.MinIdleMinutes,
        MaxIdleMinutes = AlertDefaults.MaxIdleMinutes,
    };
}