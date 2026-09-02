namespace ShaloTrack_API.DTOs.Geofence;

public class UpdateGeofenceDto
{
    public Guid? VehicleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int RadiusMeters { get; set; }
    public bool AlertOnEnter { get; set; }
    public bool AlertOnExit { get; set; }
    public bool IsActive { get; set; }
}