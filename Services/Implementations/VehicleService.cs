using System.Net;
using ShaloTrack_API.DTOs.Vehicle;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class VehicleService : IVehicleService
{
    private readonly IUnitOfWork _unitOfWork;

    public VehicleService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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

        return ApiResponse<VehicleResponseDto>.Ok(
            ToDto(vehicle),
            "Vehicle retrieved successfully."
        );
    }

    public async Task<ApiResponse<IReadOnlyList<VehicleResponseDto>>> GetByCustomerAsync(Guid customerId)
    {
        if (!await _unitOfWork.Customers.ExistsAsync(customerId))
        {
            return ApiResponse<IReadOnlyList<VehicleResponseDto>>.Fail(
                (int)HttpStatusCode.NotFound,
                "Customer not found.",
                "The specified customer does not exist."
            );
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

        _unitOfWork.Vehicles.Delete(vehicle);

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<string>.Ok(
            "Vehicle deleted successfully.",
            "Vehicle deleted successfully."
        );
    }

    private static VehicleResponseDto ToDto(Vehicle vehicle)
    {
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
            HasGpsDevice = vehicle.DeviceAssignments.Any()
        };
    }
}