using ShaloTrack_API.Models;
using ShaloTrack_API.DTOs.Dashboard;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface ICustomerRepository
{
    Task<DashboardResponseDto?> GetDashboardAsync(Guid customerId);
    Task<List<Customer>> GetAllAsync();

    Task<Customer?> GetByIdAsync(Guid customerId);

    Task<Customer?> GetByEmailAsync(string email);

    Task<Customer?> GetByNicAsync(string nicNumber);

    Task<bool> ExistsAsync(Guid customerId);

    Task AddAsync(Customer customer);

    void Update(Customer customer);

    void Delete(Customer customer);
}