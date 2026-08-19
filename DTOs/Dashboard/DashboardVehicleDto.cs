namespace ShaloTrack_API.DTOs.Dashboard;

public class DashboardVehicleDto
{
    public Guid VehicleId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public Guid? DeviceId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal Speed { get; set; }
    public decimal Heading { get; set; }
    public bool Online { get; set; }
    public bool Ignition { get; set; }
    public DateTime? LastUpdate { get; set; }

    // NEW -- Vehicle Sharing. False for vehicles the customer actually
    // owns, true for vehicles merged in from an Accepted share. The
    // Android side uses this to hide owner-only actions (delete, edit,
    // Immobilize, etc.) for shared vehicles -- a viewer having "full
    // access" was scoped as live-tracking/alerts, not the ability to
    // modify or remove someone else's vehicle.
    public bool IsShared { get; set; }
    public string? OwnerName { get; set; }
}