using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IAlertRepository
{
    // NEW: vehicleId is optional -- null means "all of this customer's
    // vehicles" (unchanged existing behavior), a real value filters to
    // just that one. Safe by construction: still combined with the
    // CustomerId check in the implementation, so a vehicleId belonging to
    // someone else just returns an empty list, not another customer's data.
    Task<List<Alert>> GetByCustomerAsync(Guid customerId, int page, int pageSize, Guid? vehicleId = null);
    Task<Alert?> GetByIdAsync(long alertId);
    Task AddAsync(Alert alert);

    /// <summary>
    /// Most recent alert of a given type for a device, strictly before the given
    /// time. Built for trip-start resolution: given an IgnitionOff at time T,
    /// this finds the IgnitionOn that (candidately) opened the trip. Backed by
    /// the IX_Alerts_DeviceId_AlertType_TriggeredAt composite index (Phase 1).
    /// </summary>
    Task<Alert?> GetMostRecentByDeviceAndTypeAsync(Guid deviceId, AlertType alertType, DateTime before);

    /// <summary>
    /// True if an alert of the given type exists for this device strictly
    /// between the two times (exclusive both ends). Used to detect an
    /// intervening IgnitionOff between a candidate trip-start IgnitionOn and
    /// the trip-end being archived -- without this check, a stale IgnitionOn
    /// from days/weeks earlier can get incorrectly paired with today's
    /// IgnitionOff, producing a bogus multi-day "trip" (see chat: the
    /// WP CAD 9934 test that swept 7 days of idle pings into one archive).
    /// </summary>
    Task<bool> ExistsByDeviceAndTypeBetweenAsync(Guid deviceId, AlertType alertType, DateTime after, DateTime before);
}