using System.Net;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.Geofence;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class GeofenceService : IGeofenceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public GeofenceService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<GeofenceResponseDto>>> GetMineAsync()
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
        {
            return ApiResponse<IReadOnlyList<GeofenceResponseDto>>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var ownGeofences = await _unitOfWork.Geofences.GetByCustomerAsync(customer.CustomerId);
        var dtoList = ownGeofences.Select(g => ToDto(g, isOwner: true)).ToList();

        // NEW -- real gap found and fixed: geofence Enter/Exit alerts
        // already reach an accepted shared viewer (via the same push
        // pipeline used for every other alert type), but until this fix
        // they had no way to actually SEE the geofence itself -- a
        // confusing experience (a push saying "Entered geofence 'Home'"
        // with nothing to show where "Home" even is). Same "full access
        // = view, not structural changes" boundary as everywhere else:
        // visible here, but UpdateAsync/DeleteAsync below remain
        // owner-only, unaffected by this.
        var acceptedShares = await _unitOfWork.VehicleShares.GetSharedWithMeAsync(customer.CustomerId);
        foreach (var share in acceptedShares)
        {
            var ownerGeofences = await _unitOfWork.Geofences.GetForVehicleAsync(share.OwnerCustomerId, share.VehicleId);
            dtoList.AddRange(ownerGeofences.Select(g => ToDto(g, isOwner: false)));
        }

        // NEW -- the shared demo vehicle's geofences (if any) are visible
        // to every customer too, same reasoning as the shared-vehicle
        // merge above. Skipped for whoever the demo vehicle's actual
        // recorded owner is, since their own geofences are already
        // included via ownGeofences above.
        var demoVehicle = await _unitOfWork.Vehicles.GetDemoVehicleAsync();
        if (demoVehicle is not null && demoVehicle.CustomerId != customer.CustomerId)
        {
            var demoGeofences = await _unitOfWork.Geofences.GetForVehicleAsync(demoVehicle.CustomerId, demoVehicle.VehicleId);
            dtoList.AddRange(demoGeofences.Select(g => ToDto(g, isOwner: false)));
        }

        return ApiResponse<IReadOnlyList<GeofenceResponseDto>>.Ok(dtoList, "Geofences retrieved successfully.");
    }

    public async Task<ApiResponse<GeofenceResponseDto>> CreateAsync(CreateGeofenceDto dto)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
        {
            return ApiResponse<GeofenceResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ApiResponse<GeofenceResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Name is required.", "A geofence needs a name.");
        }

        if (dto.RadiusMeters <= 0)
        {
            return ApiResponse<GeofenceResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Invalid radius.", "Radius must be greater than zero.");
        }

        // Real ownership check on the vehicle scope -- learned directly
        // from today's audit findings elsewhere in this API. A customer
        // must not be able to scope a geofence to a vehicle they don't
        // own, even though the effect here is only "which vehicle
        // triggers alerts", not exposing another customer's data.
        if (dto.VehicleId.HasValue)
        {
            var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(dto.VehicleId.Value);
            if (vehicle is null || vehicle.CustomerId != customer.CustomerId)
            {
                return ApiResponse<GeofenceResponseDto>.Fail(
                    (int)HttpStatusCode.NotFound, "Vehicle not found.", $"No vehicle exists with ID '{dto.VehicleId.Value}'.");
            }
        }

        var geofence = new Geofence
        {
            GeofenceId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            VehicleId = dto.VehicleId,
            Name = dto.Name.Trim(),
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            RadiusMeters = dto.RadiusMeters,
            // Both default true if the client omits them entirely --
            // matches the confirmed default. An explicit false is
            // respected as-is.
            AlertOnEnter = dto.AlertOnEnter ?? true,
            AlertOnExit = dto.AlertOnExit ?? true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Geofences.AddAsync(geofence);
        await _unitOfWork.SaveChangesAsync();

        geofence.Vehicle = dto.VehicleId.HasValue ? await _unitOfWork.Vehicles.GetByIdAsync(dto.VehicleId.Value) : null;

        return ApiResponse<GeofenceResponseDto>.Ok(ToDto(geofence, isOwner: true), "Geofence created.");
    }

    public async Task<ApiResponse<GeofenceResponseDto>> UpdateAsync(Guid geofenceId, UpdateGeofenceDto dto)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
        {
            return ApiResponse<GeofenceResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var geofence = await _unitOfWork.Geofences.GetByIdAsync(geofenceId);
        if (geofence is null || (!_currentUser.IsStaff && geofence.CustomerId != customer.CustomerId))
        {
            // Same 404-not-403 pattern used everywhere else in this API.
            return ApiResponse<GeofenceResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Geofence not found.", $"No geofence exists with ID '{geofenceId}'.");
        }

        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return ApiResponse<GeofenceResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Name is required.", "A geofence needs a name.");
        }

        if (dto.RadiusMeters <= 0)
        {
            return ApiResponse<GeofenceResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Invalid radius.", "Radius must be greater than zero.");
        }

        if (dto.VehicleId.HasValue)
        {
            var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(dto.VehicleId.Value);
            if (vehicle is null || vehicle.CustomerId != customer.CustomerId)
            {
                return ApiResponse<GeofenceResponseDto>.Fail(
                    (int)HttpStatusCode.NotFound, "Vehicle not found.", $"No vehicle exists with ID '{dto.VehicleId.Value}'.");
            }
        }

        geofence.VehicleId = dto.VehicleId;
        geofence.Name = dto.Name.Trim();
        geofence.Latitude = dto.Latitude;
        geofence.Longitude = dto.Longitude;
        geofence.RadiusMeters = dto.RadiusMeters;
        geofence.AlertOnEnter = dto.AlertOnEnter;
        geofence.AlertOnExit = dto.AlertOnExit;
        geofence.IsActive = dto.IsActive;

        _unitOfWork.Geofences.Update(geofence);
        await _unitOfWork.SaveChangesAsync();

        geofence.Vehicle = dto.VehicleId.HasValue ? await _unitOfWork.Vehicles.GetByIdAsync(dto.VehicleId.Value) : null;

        return ApiResponse<GeofenceResponseDto>.Ok(ToDto(geofence, isOwner: true), "Geofence updated.");
    }

    public async Task<ApiResponse<string>> DeleteAsync(Guid geofenceId)
    {
        var customer = await ResolveCustomerAsync();
        if (customer is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "No valid session found.");
        }

        var geofence = await _unitOfWork.Geofences.GetByIdAsync(geofenceId);
        if (geofence is null || (!_currentUser.IsStaff && geofence.CustomerId != customer.CustomerId))
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound, "Geofence not found.", $"No geofence exists with ID '{geofenceId}'.");
        }

        _unitOfWork.Geofences.Remove(geofence);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<string>.Ok("OK", "Geofence removed.");
    }

    private async Task<Customer?> ResolveCustomerAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid)) return null;
        return await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
    }

    private static GeofenceResponseDto ToDto(Geofence geofence, bool isOwner)
    {
        return new GeofenceResponseDto
        {
            GeofenceId = geofence.GeofenceId,
            VehicleId = geofence.VehicleId,
            VehicleNumber = geofence.Vehicle?.VehicleNumber,
            Name = geofence.Name,
            Latitude = geofence.Latitude,
            Longitude = geofence.Longitude,
            RadiusMeters = geofence.RadiusMeters,
            AlertOnEnter = geofence.AlertOnEnter,
            AlertOnExit = geofence.AlertOnExit,
            IsActive = geofence.IsActive,
            CreatedAt = geofence.CreatedAt,
            IsOwner = isOwner
        };
    }
}