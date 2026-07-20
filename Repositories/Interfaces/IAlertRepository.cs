using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IAlertRepository
{
    Task<List<Alert>> GetByCustomerAsync(Guid customerId, int page, int pageSize);
    Task<Alert?> GetByIdAsync(long alertId);
    Task AddAsync(Alert alert);
}