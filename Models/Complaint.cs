using System.ComponentModel.DataAnnotations;
using ShaloTrack_API.Enums;

namespace ShaloTrack_API.Models;

public class Complaint
{
    [Key]
    public Guid ComplaintId { get; set; }

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    // Required, not nullable -- this feature is explicitly per-device:
    // every complaint is filed against a specific vehicle/device the
    // customer owns, not a general account-level complaint.
    public Guid VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    public ComplaintCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;

    public ComplaintStatus Status { get; set; }

    // DealerId is a plain reference value, NOT a real foreign key -- the
    // actual Dealer table lives in Admin's separate database, same
    // pattern already established for SetupShalotrackDevices.DealerId.
    // Resolved once, at filing time, via a real-time lookup call to
    // Admin's dealer-matching endpoint (phone/email soft-match, since
    // that's the only place this relationship can be computed). Stays
    // populated even after a dealer escalates to admin -- it records
    // who the complaint came via, not who currently owns handling it.
    public int? DealerId { get; set; }

    // Denormalized at the same time as DealerId, purely for display --
    // the API has no way to join against Admin's Dealer table directly,
    // so the name is captured once rather than requiring every read to
    // re-resolve it.
    public string? DealerName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Set only when a dealer explicitly transfers a WithDealer complaint
    // to WithAdmin because they couldn't resolve it themselves. Null for
    // complaints that started as WithAdmin (no dealer matched at all).
    public DateTime? EscalatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public ICollection<ComplaintReply> Replies { get; set; } = new List<ComplaintReply>();
}