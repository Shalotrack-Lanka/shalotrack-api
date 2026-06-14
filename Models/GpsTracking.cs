using System.ComponentModel.DataAnnotations;
namespace ShaloTrack_API.Models;

public class GpsTracking
{
    [Key]
    public long TrackingId { get; set; }
    public Guid DeviceId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? Altitude { get; set; }
    public decimal Speed { get; set; }
    public decimal Heading { get; set; }
    public int Satellites { get; set; }
    public decimal? GpsAccuracy { get; set; }
    public DateTime EventTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public GpsDevice Device { get; set; } = null!;
}