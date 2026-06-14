using System.ComponentModel.DataAnnotations;
namespace ShaloTrack_API.Models;

public class CurrentLocation
{
    [Key]
    public Guid DeviceId { get; set; }
    public Guid VehicleId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal Speed { get; set; }
    public decimal Heading { get; set; }
    public bool IgnitionStatus { get; set; }
    public bool MovementStatus { get; set; }
    public DateTime LastUpdate { get; set; }
    public GpsDevice Device { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
}