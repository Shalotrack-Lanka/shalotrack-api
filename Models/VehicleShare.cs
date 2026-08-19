using System.ComponentModel.DataAnnotations;
using ShaloTrack_API.Enums;

namespace ShaloTrack_API.Models;

public class VehicleShare
{
    [Key]
    public Guid ShareId { get; set; }

    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    // The vehicle's owner -- denormalized here even though it's derivable
    // via Vehicle.CustomerId, since ownership checks on this table happen
    // far more often than full Vehicle joins, and a share should never
    // silently follow a vehicle to a new owner if VehicleId is ever
    // reassigned.
    public Guid OwnerCustomerId { get; set; }
    public Customer OwnerCustomer { get; set; } = null!;

    // Resolved directly at invite time -- the owner invites by phone
    // number, and since sharing requires the other person to already
    // have the app installed, a matching Customer is looked up and
    // linked immediately rather than storing an unresolved phone number
    // and reconciling it later.
    public Guid SharedWithCustomerId { get; set; }
    public Customer SharedWithCustomer { get; set; } = null!;

    public VehicleShareStatus Status { get; set; } = VehicleShareStatus.Pending;

    public DateTime InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}