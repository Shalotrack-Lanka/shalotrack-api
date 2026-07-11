using ShaloTrack_API.DTOs.Customer;
using ShaloTrack_API.DTOs.Dashboard;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Implementations;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;
using System.Net;
using ShaloTrack_API.Auth;

namespace ShaloTrack_API.Services.Implementations;

public class CustomerService : ICustomerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    //constructor method
    public CustomerService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<ApiResponse<CustomerResponseDto>> GetMyProfileAsync()
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<CustomerResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized,
                "Authentication required.",
                "No valid session found."
            );
        }

        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(uid);
        if (customer is null)
        {
            return ApiResponse<CustomerResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Profile not found.",
                "No customer profile exists for this account yet."
            );
        }

        return ApiResponse<CustomerResponseDto>.Ok(ToDto(customer), "Profile retrieved successfully.");
    }

    public async Task<ApiResponse<IReadOnlyList<CustomerResponseDto>>> GetAllAsync()
    {
        // Retrieve entities
        var customers = await _unitOfWork.Customers.GetAllAsync();

        // Map entities
        var dtoList = customers
            .Select(ToDto)
            .ToList();

        // Return response
        return ApiResponse<IReadOnlyList<CustomerResponseDto>>.Ok(
            dtoList,
            "Customers retrieved successfully."
        );
    }

    public async Task<ApiResponse<CustomerResponseDto>> GetByIdAsync(Guid customerId)
    {
        // Retrieve entity
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);

        // Validation
        if (customer is null)
        {
            return ApiResponse<CustomerResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Customer not found.",
                $"No customer exists with ID '{customerId}'."
            );
        }

        // Return response
        return ApiResponse<CustomerResponseDto>.Ok(
            ToDto(customer),
            "Customer retrieved successfully."
        );
    }

    public async Task<ApiResponse<CustomerResponseDto>> CreateAsync(CreateCustomerDto dto)
    {
        // Business validation
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid))
        {
            return ApiResponse<CustomerResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized,
                "Authentication required.",
                "A verified account is required to create a customer profile."
            );
        }

        if (await _unitOfWork.Customers.GetByFirebaseUidAsync(uid) is not null)
        {
            return ApiResponse<CustomerResponseDto>.Fail(
                (int)HttpStatusCode.Conflict,
                "Profile already exists.",
                "This account already has a customer profile."
            );
        }

        if (await _unitOfWork.Customers.GetByEmailAsync(dto.Email) is not null)
        {
            return ApiResponse<CustomerResponseDto>.Fail(
                (int)HttpStatusCode.Conflict,
                "Email already exists.",
                "A customer with this email address already exists."
            );
        }

        if (await _unitOfWork.Customers.GetByNicAsync(dto.NicNumber) is not null)
        {
            return ApiResponse<CustomerResponseDto>.Fail(
                (int)HttpStatusCode.Conflict,
                "NIC already exists.",
                "A customer with this NIC already exists."
            );
        }

        // Create entity
        var customer = new Customer
        {
            CustomerId = Guid.NewGuid(),
            FirebaseUid = uid,
            FullName = dto.FullName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            NicNumber = dto.NicNumber,
            Address = dto.Address,
            AccountStatus = CustomerStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Persist
        await _unitOfWork.Customers.AddAsync(customer);
        await _unitOfWork.SaveChangesAsync();

        // Return response
        return ApiResponse<CustomerResponseDto>.Ok(
            ToDto(customer),
            "Customer created successfully."
        );
    }

    public async Task<ApiResponse<CustomerResponseDto>> UpdateAsync(
        Guid customerId,
        UpdateCustomerDto dto)
    {
        // Retrieve entity
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);

        // Validation
        if (customer is null)
        {
            return ApiResponse<CustomerResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Customer not found.",
                $"No customer exists with ID '{customerId}'."
            );
        }

        // Update entity
        customer.FullName = dto.FullName;
        customer.PhoneNumber = dto.PhoneNumber;
        customer.Address = dto.Address;
        customer.ProfileImage = dto.ProfileImage;
        customer.UpdatedAt = DateTime.UtcNow;

        // Persist
        _unitOfWork.Customers.Update(customer);
        await _unitOfWork.SaveChangesAsync();

        // Return response
        return ApiResponse<CustomerResponseDto>.Ok(
            ToDto(customer),
            "Customer updated successfully."
        );
    }

    public async Task<ApiResponse<string>> DeactivateAsync(Guid customerId)
    {
        // Retrieve entity
        var customer = await _unitOfWork.Customers.GetByIdAsync(customerId);

        // Validation
        if (customer is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound,
                "Customer not found.",
                $"No customer exists with ID '{customerId}'."
            );
        }

        // Business logic
        customer.AccountStatus = CustomerStatus.Inactive;
        customer.UpdatedAt = DateTime.UtcNow;

        // Persist
        _unitOfWork.Customers.Update(customer);
        await _unitOfWork.SaveChangesAsync();

        // Return response
        return ApiResponse<string>.Ok(
            "Customer deactivated successfully.",
            "Customer deactivated successfully."
        );
    }

    private static CustomerResponseDto ToDto(Customer customer)
    {
        return new CustomerResponseDto
        {
            CustomerId = customer.CustomerId,
            FullName = customer.FullName,
            Email = customer.Email,
            PhoneNumber = customer.PhoneNumber,
            NicNumber = customer.NicNumber,
            Address = customer.Address,
            ProfileImage = customer.ProfileImage,
            AccountStatus = customer.AccountStatus,
            VehicleCount = customer.Vehicles.Count
        };
    }

    public async Task<ApiResponse<DashboardResponseDto>> GetDashboardAsync(
    Guid customerId)
    {
        var dashboard = await _unitOfWork.Customers
            .GetDashboardAsync(customerId);

        if (dashboard == null)
        {
            return ApiResponse<DashboardResponseDto>.Fail(
                404,
                "Customer not found.");
        }

        return ApiResponse<DashboardResponseDto>.Ok(
            dashboard,
            "Dashboard retrieved successfully.");
    }
}