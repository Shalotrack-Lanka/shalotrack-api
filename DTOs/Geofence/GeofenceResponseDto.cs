namespace ShaloTrack_API.DTOs.Geofence;

public class GeofenceResponseDto
{
    public Guid GeofenceId { get; set; }
    public Guid? VehicleId { get; set; }

    // Populated only when VehicleId is set, for display convenience --
    // null for an "all vehicles" geofence.
    public string? VehicleNumber { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int RadiusMeters { get; set; }
    public bool AlertOnEnter { get; set; }
    public bool AlertOnExit { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}