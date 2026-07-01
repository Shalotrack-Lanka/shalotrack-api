using ShaloTrack_API.DTOs.Customer;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface ICustomerService
{
    Task<ApiResponse<IReadOnlyList<CustomerResponseDto>>> GetAllAsync();

    Task<ApiResponse<CustomerResponseDto>> GetByIdAsync(Guid customerId);

    Task<ApiResponse<CustomerResponseDto>> CreateAsync(CreateCustomerDto dto);

    Task<ApiResponse<CustomerResponseDto>> UpdateAsync(
        Guid customerId,
        UpdateCustomerDto dto);

    Task<ApiResponse<string>> DeactivateAsync(Guid customerId);
}