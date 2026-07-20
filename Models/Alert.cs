using ShaloTrack_API.Enums;
using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

public class Alert
{
    [Key]
    public long AlertId { get; set; }

    public Guid VehicleId { get; set; }
    public Guid? DeviceId { get; set; }

    public AlertType AlertType { get; set; }
    public string Message { get; set; } = string.Empty;

    // Location at the time the alert fired, if relevant (null for e.g. LowBattery).
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    public DateTime TriggeredAt { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
}