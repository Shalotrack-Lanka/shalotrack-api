using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.Geofence;

public class UpdateGeofenceDto
{
    public Guid? VehicleId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "-90", "90")]
    public decimal Latitude { get; set; }

    [Range(typeof(decimal), "-180", "180")]
    public decimal Longitude { get; set; }

    [Range(1, 50000)]
    public int RadiusMeters { get; set; }

    public bool AlertOnEnter { get; set; }
    public bool AlertOnExit { get; set; }
    public bool IsActive { get; set; }
}