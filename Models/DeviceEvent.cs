using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

public class DeviceEvent
{
    [Key]
    public long EventId { get; set; }
    public Guid DeviceId { get; set; }
    public Guid? VehicleId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public short Severity { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public long? RawPacketId { get; set; }
    public string? Description { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public GpsDevice Device { get; set; } = null!;
    public Vehicle? Vehicle { get; set; }
    public RawPacket? RawPacket { get; set; }
}