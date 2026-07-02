using ShaloTrack_API.DTOs.Vehicle;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IVehicleService
{
    Task<ApiResponse<IReadOnlyList<VehicleResponseDto>>> GetAllAsync();

    Task<ApiResponse<VehicleResponseDto>> GetByIdAsync(Guid vehicleId);

    Task<ApiResponse<IReadOnlyList<VehicleResponseDto>>> GetByCustomerAsync(Guid customerId);

    Task<ApiResponse<VehicleResponseDto>> CreateAsync(CreateVehicleDto dto);

    Task<ApiResponse<VehicleResponseDto>> UpdateAsync(Guid vehicleId, UpdateVehicleDto dto);

    Task<ApiResponse<string>> DeleteAsync(Guid vehicleId);
}