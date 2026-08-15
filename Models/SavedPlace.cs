using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

public class SavedPlace
{
    [Key]
    public Guid PlaceId { get; set; }

    public Guid CustomerId { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }

    // How close a position needs to be to count as "at this place". Default
    // set server-side (see SavedPlaceService), not client-supplied -- keeps
    // this a deliberate, consistent value rather than something a client
    // request could set arbitrarily small/large.
    public int RadiusMeters { get; set; }

    // Incremented by LocationNotificationListener on each detected ENTER
    // transition (arriving at the place after not being there), same
    // transition-detection pattern already used for Ignition/Overspeed/
    // PowerCut/LowBattery -- not incremented on every GPS ping while
    // continuously parked there.
    public int VisitCount { get; set; }

    public DateTime? LastVisitedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
}