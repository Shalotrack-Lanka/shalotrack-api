using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ShaloTrack_API.Auth;
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
            if (vehicle is null ||
                !string.Equals(vehicle.Customer?.FirebaseUid, _currentUser.FirebaseUid, StringComparison.Ordinal))
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