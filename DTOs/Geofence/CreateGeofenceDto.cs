using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.Geofence;

public class CreateGeofenceDto
{
    public Guid? VehicleId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "-90", "90")]
    public decimal Latitude { get; set; }

    [Range(typeof(decimal), "-180", "180")]
    public decimal Longitude { get; set; }

    // Upper cap only (Fleet allows up to 50 km, Android up to 5 km). The lower bound stays at 1 so geofences
    // created before these limits can still be edited; GeofenceService already rejects 0 and below.
    [Range(1, 50000)]
    public int RadiusMeters { get; set; }

    public bool? AlertOnEnter { get; set; }
    public bool? AlertOnExit { get; set; }
}