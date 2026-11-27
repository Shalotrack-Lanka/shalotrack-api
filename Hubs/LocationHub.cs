using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ShaloTrack_API.Auth;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Hubs;

/// <summary>
/// Real-time push channel for live vehicle locations. Authenticated via the same Firebase JWT scheme as REST
/// endpoints. Clients join a group named after the vehicleId they want updates for. JoinVehicleGroup applies the
/// same rule as the REST location endpoints: the owner, a customer with an ACCEPTED share, anyone for the demo
/// vehicle, and staff for every vehicle.
///
/// Access is not only checked at join: every customer membership is recorded in <see cref="LiveGroupRegistry"/>
/// and re-checked once a minute by LiveAccessSweeper, so a revoked share stops receiving positions within about a
/// minute even if the viewer's app never disconnects. The removed connection gets an "AccessEnded" message with
/// the vehicle id so an honest client can close the screen straight away.
/// </summary>
[Authorize]
public class LocationHub : Hub
{
    public const string AccessEndedEvent = "AccessEnded";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly ISubscriptionGateService _subscriptionGate;
    private readonly LiveGroupRegistry _registry;

    public LocationHub(IUnitOfWork unitOfWork, ICurrentUser currentUser, ISubscriptionGateService subscriptionGate,
        LiveGroupRegistry registry)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _subscriptionGate = subscriptionGate;
        _registry = registry;
    }

    /// <summary>The push side sends to <c>Guid.ToString()</c> (lower case), so every join and leave uses that
    /// exact spelling; a client that sent upper case used to join a group nothing was ever sent to.</summary>
    public static string GroupName(Guid vehicleId) => vehicleId.ToString();

    public async Task JoinVehicleGroup(string vehicleId)
    {
        if (!Guid.TryParse(vehicleId, out var vehicleGuid))
        {
            throw new HubException("Invalid vehicleId.");
        }

        string? firebaseUid = _currentUser.FirebaseUid;
        if (!_currentUser.IsStaff)
        {
            if (string.IsNullOrEmpty(firebaseUid))
            {
                throw new HubException("Vehicle not found.");
            }

            // Ownership needs only the owner's FirebaseUid: the light lookup, not the one that also loads every
            // device assignment.
            var vehicle = await _unitOfWork.Vehicles.GetByIdForOwnershipCheckAsync(vehicleGuid);
            bool isOwner = vehicle is not null &&
                string.Equals(vehicle.Customer?.FirebaseUid, firebaseUid, StringComparison.Ordinal);

            bool hasAcceptedShare = false;
            if (!isOwner && vehicle is not null && !vehicle.IsDemoVehicle)
            {
                var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(firebaseUid);
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

        if (await _subscriptionGate.IsRenewalRequiredAsync(vehicleGuid))
        {
            throw new HubException(ISubscriptionGateService.RenewalRequiredCode);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(vehicleGuid));
        if (!_currentUser.IsStaff && !string.IsNullOrEmpty(firebaseUid))
        {
            _registry.Add(Context.ConnectionId, vehicleGuid, firebaseUid);
        }
    }

    public async Task LeaveVehicleGroup(string vehicleId)
    {
        if (!Guid.TryParse(vehicleId, out var vehicleGuid))
        {
            return;
        }
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(vehicleGuid));
        _registry.Remove(Context.ConnectionId, vehicleGuid);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // SignalR drops a closed connection from its groups by itself; this keeps the registry in step.
        _registry.RemoveConnection(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
