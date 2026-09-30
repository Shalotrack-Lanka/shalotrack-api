using ShaloTrack_API.Auth;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class SubscriptionGateService : ISubscriptionGateService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISubscriptionGateRepository _gate;
    private readonly ICurrentUser _currentUser;

    public SubscriptionGateService(
        IUnitOfWork unitOfWork,
        ISubscriptionGateRepository gate,
        ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _gate = gate;
        _currentUser = currentUser;
    }

    public async Task<bool> IsRenewalRequiredAsync(Guid vehicleId)
    {
        if (_currentUser.IsStaff) return false;

        var vehicle = await _unitOfWork.Vehicles.GetByIdForOwnershipCheckAsync(vehicleId);
        if (vehicle is null || vehicle.IsDemoVehicle) return false;

        var assignments = await _unitOfWork.DeviceAssignments.GetByVehicleAsync(vehicleId, activeOnly: true);
        var imei = assignments
            .OrderByDescending(a => a.AssignedAt)
            .Select(a => a.Device?.ImeiNumber)
            .FirstOrDefault(i => !string.IsNullOrWhiteSpace(i));
        if (imei is null) return false;

        var status = await _gate.GetByImeiAsync(imei);
        if (status is null) return false;

        // The admin clears the expiry date when it marks a device not-Paid, so "not active"
        // covers both lapsed and never-paid devices. The expiry check covers the gap between
        // the end date passing and the nightly expiry job running.
        return !status.IsActive
            || (status.ExpiresAt.HasValue && status.ExpiresAt.Value <= DateTime.UtcNow);
    }
}