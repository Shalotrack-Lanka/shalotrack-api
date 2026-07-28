using System.Net;
using ShaloTrack_API.DTOs.Vehicle;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;
using ShaloTrack_API.Auth;

namespace ShaloTrack_API.Services.Implementations;

public class VehicleService : IVehicleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public VehicleService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<IReadOnlyList<VehicleResponseDto>>> GetAllAsync()
    {
        var vehicles = await _unitOfWork.Vehicles.GetAllAsync();

        var dtoList = vehicles
            .Select(ToDto)
            .ToList();

        return ApiResponse<IReadOnlyList<VehicleResponseDto>>.Ok(
            dtoList,
            "Vehicles retrieved successfully."
        );
    }

    public async Task<ApiResponse<VehicleResponseDto>> GetByIdAsync(Guid vehicleId)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);

        if (vehicle is null)
        {
            return ApiResponse<VehicleResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Vehicle not found.",
                $"No vehicle exists with ID '{vehicleId}'."
            );
        }

        //vehicle.customer is already included

        if (!_currentUser.IsStaff &&
        !string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal))
        {
            return ApiResponse<VehicleResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Vehicle not found.",
                $"No vehicle exists with ID '{vehicleId}'.");
        }

        return ApiResponse<VehicleResponseDto>.Ok(
            ToDto(vehicle),
            "Vehicle retrieved successfully."
        );
    }

    public async Task<ApiResponse<IReadOnlyList<VehicleResponseDto>>> GetByCustomerAsync(Guid customerId)
    {
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);

        if (customer is null ||
            (!_currentUser.IsStaff &&
             !string.Equals(customer.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal)))
        {
            return ApiResponse<IReadOnlyList<VehicleResponseDto>>.Fail(
                (int)HttpStatusCode.NotFound,
                "Customer not found.",
                "The specified customer does not exist.");
        }

        var vehicles = await _unitOfWork.Vehicles.GetByCustomerAsync(customerId);

        var dtoList = vehicles
            .Select(ToDto)
            .ToList();

        return ApiResponse<IReadOnlyList<VehicleResponseDto>>.Ok(
            dtoList,
            "Vehicles retrieved successfully."
        );
    }

    public async Task<ApiResponse<VehicleResponseDto>> CreateAsync(CreateVehicleDto dto)
    {
        if (!await _unitOfWork.Customers.ExistsAsync(dto.CustomerId))
        {
            return ApiResponse<VehicleResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Customer not found.",
                "The specified customer does not exist."
            );
        }

        if (await _unitOfWork.Vehicles.GetByVehicleNumberAsync(dto.VehicleNumber) is not null)
        {
            return ApiResponse<VehicleResponseDto>.Fail(
                (int)HttpStatusCode.Conflict,
                "Vehicle number already exists.",
                "Vehicle number must be unique."
            );
        }

        if (!string.IsNullOrWhiteSpace(dto.ChassisNumber))
        {
            if (await _unitOfWork.Vehicles.GetByChassisNumberAsync(dto.ChassisNumber) is not null)
            {
                return ApiResponse<VehicleResponseDto>.Fail(
                    (int)HttpStatusCode.Conflict,
                    "Chassis number already exists.",
                    "Chassis number must be unique."
                );
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.EngineNumber))
        {
            if (await _unitOfWork.Vehicles.GetByEngineNumberAsync(dto.EngineNumber) is not null)
            {
                return ApiResponse<VehicleResponseDto>.Fail(
                    (int)HttpStatusCode.Conflict,
                    "Engine number already exists.",
                    "Engine number must be unique."
                );
            }
        }

        var vehicle = new Vehicle
        {
            VehicleId = Guid.NewGuid(),
            CustomerId = dto.CustomerId,
            VehicleNumber = dto.VehicleNumber,
            ChassisNumber = dto.ChassisNumber,
            EngineNumber = dto.EngineNumber,
            Make = dto.Make,
            Model = dto.Model,
            Year = dto.Year,
            Color = dto.Color,
            VehicleType = dto.VehicleType,
            FuelType = dto.FuelType,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Vehicles.AddAsync(vehicle);

        await _unitOfWork.SaveChangesAsync();

        vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicle.VehicleId)
            ?? vehicle;

        return ApiResponse<VehicleResponseDto>.Ok(
            ToDto(vehicle),
            "Vehicle created successfully."
        );
    }

    public async Task<ApiResponse<VehicleResponseDto>> UpdateAsync(Guid vehicleId, UpdateVehicleDto dto)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);

        if (vehicle is null)
        {
            return ApiResponse<VehicleResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Vehicle not found.",
                "The specified vehicle does not exist."
            );
        }

        var existingVehicle = await _unitOfWork.Vehicles.GetByVehicleNumberAsync(dto.VehicleNumber);

        if (existingVehicle is not null &&
            existingVehicle.VehicleId != vehicleId)
        {
            return ApiResponse<VehicleResponseDto>.Fail(
                (int)HttpStatusCode.Conflict,
                "Vehicle number already exists.",
                "Vehicle number must be unique."
            );
        }

        if (!string.IsNullOrWhiteSpace(dto.ChassisNumber))
        {
            var existingChassis = await _unitOfWork.Vehicles
                .GetByChassisNumberAsync(dto.ChassisNumber);

            if (existingChassis is not null &&
                existingChassis.VehicleId != vehicleId)
            {
                return ApiResponse<VehicleResponseDto>.Fail(
                    (int)HttpStatusCode.Conflict,
                    "Chassis number already exists.",
                    "Chassis number must be unique."
                );
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.EngineNumber))
        {
            var existingEngine = await _unitOfWork.Vehicles
                .GetByEngineNumberAsync(dto.EngineNumber);

            if (existingEngine is not null &&
                existingEngine.VehicleId != vehicleId)
            {
                return ApiResponse<VehicleResponseDto>.Fail(
                    (int)HttpStatusCode.Conflict,
                    "Engine number already exists.",
                    "Engine number must be unique."
                );
            }
        }

        vehicle.VehicleNumber = dto.VehicleNumber;
        vehicle.ChassisNumber = dto.ChassisNumber;
        vehicle.EngineNumber = dto.EngineNumber;
        vehicle.Make = dto.Make;
        vehicle.Model = dto.Model;
        vehicle.Year = dto.Year;
        vehicle.Color = dto.Color;
        vehicle.VehicleType = dto.VehicleType;
        vehicle.FuelType = dto.FuelType;
        vehicle.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Vehicles.Update(vehicle);

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<VehicleResponseDto>.Ok(
            ToDto(vehicle),
            "Vehicle updated successfully."
        );
    }

    public async Task<ApiResponse<string>> DeleteAsync(Guid vehicleId)
    {
        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleId);

        if (vehicle is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound,
                "Vehicle not found.",
                "The specified vehicle does not exist."
            );
        }

        if (!vehicle.IsActive)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.BadRequest,
                "Already removed.",
                "This vehicle has already been removed."
            );
        }

        // FIX: this used to hard-delete the vehicle row outright
        // (_unitOfWork.Vehicles.Delete(vehicle) + SaveChangesAsync), which
        // would either violate foreign key constraints from GpsTrackings/
        // Alerts/CurrentLocations/DeviceAssignments, or silently cascade-
        // delete all of a customer's trip and alert history along with it --
        // neither of which is acceptable. This now soft-deletes the vehicle
        // (IsActive = false, so it disappears from the owner's vehicle list
        // via VehicleRepository.GetByCustomerAsync's filter, while all
        // historical data referencing it stays intact) AND, in the same
        // transaction, unassigns its GPS device -- freeing that IMEI so it
        // can be linked to a new vehicle, by the same or a different
        // customer (e.g. after a vehicle sale), reusing the exact same
        // unassign logic as DeviceAssignmentService.UnassignAsync().
        // FIX: this previously called _unitOfWork.DeviceAssignments.GetByVehicleAsync()
        // as a separate query, which loaded a SECOND, different instance of the
        // same DeviceAssignment row that GetByIdAsync() above had already loaded
        // (untracked) via vehicle.DeviceAssignments. Calling .Update() on that
        // second instance conflicted with EF Core already tracking the first one
        // -- confirmed via a real test that threw exactly this exception. Using
        // the assignment already present on the loaded vehicle object avoids the
        // duplicate-instance conflict entirely, and no .Update() call is needed
        // at all -- EF Core auto-detects changes to entities it's already tracking.
        try
        {
            await _unitOfWork.BeginTransactionAsync();

            vehicle.IsActive = false;
            vehicle.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Vehicles.Update(vehicle);

            var activeAssignment = vehicle.DeviceAssignments
                .FirstOrDefault(a => a.Status == AssignmentStatus.Active);

            if (activeAssignment != null)
            {
                activeAssignment.Status = AssignmentStatus.Removed;
                activeAssignment.RemovedAt = DateTime.UtcNow;
            }

            await _unitOfWork.SaveChangesAsync();

            await _unitOfWork.CommitTransactionAsync();

            return ApiResponse<string>.Ok(
                "Vehicle removed successfully.",
                "Vehicle removed successfully."
            );
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private static VehicleResponseDto ToDto(Vehicle vehicle)
    {
        var activeAssignment = vehicle.DeviceAssignments
            .FirstOrDefault(a => a.Status == Enums.AssignmentStatus.Active);

        return new VehicleResponseDto
        {
            VehicleId = vehicle.VehicleId,
            CustomerId = vehicle.CustomerId,
            CustomerName = vehicle.Customer.FullName,
            VehicleNumber = vehicle.VehicleNumber,
            ChassisNumber = vehicle.ChassisNumber,
            EngineNumber = vehicle.EngineNumber,
            Make = vehicle.Make,
            Model = vehicle.Model,
            Year = vehicle.Year,
            Color = vehicle.Color,
            VehicleType = vehicle.VehicleType,
            FuelType = vehicle.FuelType,
            HasGpsDevice = activeAssignment != null,
            Imei = activeAssignment?.Device?.ImeiNumber   // NEW
        };
    }
}