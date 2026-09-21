using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IAlertRepository
{
    Task<List<Alert>> GetByCustomerAsync(Guid customerId, int page, int pageSize, Guid? vehicleId = null);
    Task<Alert?> GetByIdAsync(long alertId);
    Task AddAsync(Alert alert);

    Task<Alert?> GetMostRecentByDeviceAndTypeAsync(Guid deviceId, AlertType alertType, DateTime before);

    Task<bool> ExistsByDeviceAndTypeBetweenAsync(Guid deviceId, AlertType alertType, DateTime after, DateTime before);

    Task<int> CountByVehicleAndTypeAsync(Guid vehicleId, AlertType alertType, DateTime from, DateTime to);

    /// <summary>
    /// NEW -- report generation feature (Alert Report). Vehicle-scoped like
    /// CountByVehicleAndTypeAsync above -- AlertService resolves and verifies
    /// ownership of the specific vehicle before calling this, so there's no
    /// customerId check needed here. Unpaged (bounded by the service's own
    /// date-range cap instead), ordered most-recent-first to match
    /// GetByCustomerAsync's existing convention.
    /// </summary>
    Task<List<Alert>> GetByVehicleAndDateRangeAsync(Guid vehicleId, DateTime from, DateTime to);
}