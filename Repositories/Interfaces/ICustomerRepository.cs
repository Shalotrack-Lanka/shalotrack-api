using ShaloTrack_API.DTOs.Dashboard;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface ICustomerRepository
{
    Task<List<Customer>> GetAllAsync();
    Task<Customer?> GetByIdAsync(Guid customerId);
    Task<Customer?> GetByEmailAsync(string email);
    Task<Customer?> GetByNicAsync(string nicNumber);
    Task<bool> ExistsAsync(Guid customerId);
    Task AddAsync(Customer customer);
    void Update(Customer customer);
    void Delete(Customer customer);
    Task<DashboardResponseDto?> GetDashboardAsync(Guid customerId);
}