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

    // The number the owner typed, as E.164 (+947XXXXXXXX). An invite is addressed to a NUMBER, not to an account:
    // the inviter is never told whether that number has a ShaloTrack account (that answer would let anyone check
    // any number), so a pending invite may point at a number nobody has registered yet. Whoever signs in with
    // this number sees the invite in Sharing. Max 20 (DbContext).
    public string InvitedPhoneNumber { get; set; } = string.Empty;

    // Set when the invite is accepted: the customer who accepted it. Null while Pending or Declined (and for an
    // expired invite), because until then there is no confirmed person, only a number.
    public Guid? SharedWithCustomerId { get; set; }
    public Customer? SharedWithCustomer { get; set; }

    public VehicleShareStatus Status { get; set; } = VehicleShareStatus.Pending;

    public DateTime InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>A pending invite stops being offered after this long, so a number that later changes hands
    /// (operators recycle numbers) doesn't inherit someone else's invite.</summary>
    public static readonly TimeSpan PendingLifetime = TimeSpan.FromDays(14);
}