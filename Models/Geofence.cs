using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

public class Geofence
{
    [Key]
    public Guid GeofenceId { get; set; }

    public Guid CustomerId { get; set; }

    // Nullable by design -- null means this geofence applies to every one
    // of the customer's vehicles; a specific VehicleId scopes it to just
    // that one. Confirmed as the right default: simple ("watch all my
    // vehicles") with real flexibility available (a targeted geofence for
    // just one vehicle) without needing a many-to-many table.
    public Guid? VehicleId { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int RadiusMeters { get; set; }

    // Both true by default per direct confirmation -- alert on both
    // entering and leaving the circle. Independently toggleable per
    // geofence after creation.
    public bool AlertOnEnter { get; set; } = true;
    public bool AlertOnExit { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
    public Vehicle? Vehicle { get; set; }
}