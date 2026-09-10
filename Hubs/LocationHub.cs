using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ShaloTrack_API.Auth;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Hubs;

/// <summary>
/// Real-time push channel for live vehicle locations. Authenticated via the same
/// Firebase JWT scheme as REST endpoints. Clients join a group named after the
/// vehicleId they want updates for -- JoinVehicleGroup enforces the same ownership
/// rule as [OwnsCustomer]/the service-layer checks elsewhere: a customer can only
/// subscribe to their own vehicle, staff can subscribe to any.
/// </summary>
[Authorize]
public class LocationHub : Hub
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    public LocationHub(IUnitOfWork unitOfWork, ICurrentUser currentUser)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task JoinVehicleGroup(string vehicleId)
    {
        if (!Guid.TryParse(vehicleId, out var vehicleGuid))
        {
            throw new HubException("Invalid vehicleId.");
        }

        if (!_currentUser.IsStaff)
        {
            var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(vehicleGuid);
            bool isOwner = vehicle is not null &&
                string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal);

            // FIX: real gap found while reviewing this hub for the
            // SignalR polling-suppression work -- this only ever allowed
            // owner or staff, missing the same "accepted share" extension
            // already applied consistently to every other read-access
            // endpoint in this API (CurrentLocationService,
            // GpsTrackingService, RoadSnappingService,
            // VehicleStatsService, VehicleService.GetByIdAsync). A shared
            // viewer would have been silently blocked from ever joining
            // the live-tracking group for a vehicle they legitimately
            // have access to.
            bool hasAcceptedShare = false;
            if (!isOwner && vehicle is not null)
            {
                var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(_currentUser.FirebaseUid ?? string.Empty);
                if (customer is not null)
                {
                    var share = await _unitOfWork.VehicleShares.GetByVehicleAndSharedWithAsync(vehicleGuid, customer.CustomerId);
                    hasAcceptedShare = share is not null && share.Status == VehicleShareStatus.Accepted;
                }
            }

            if (!isOwner && !hasAcceptedShare && !(vehicle?.IsDemoVehicle ?? false))
            {
                // Same principle as REST: don't confirm existence to a non-owner.
                throw new HubException("Vehicle not found.");
            }
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, vehicleId);
    }

    public async Task LeaveVehicleGroup(string vehicleId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, vehicleId);
    }
}