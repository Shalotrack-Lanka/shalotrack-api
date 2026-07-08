using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

public class Vehicle
{
    //primary key
    [Key]
    public Guid VehicleId { get; set; }
    public Guid CustomerId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string? ChassisNumber { get; set; }
    public string? EngineNumber { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Color { get; set; }
    public string? VehicleType { get; set; }
    public string? FuelType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Customer Customer { get; set; } = null!;
    public CurrentLocation? CurrentLocation { get; set; }

    public ICollection<DeviceAssignment> DeviceAssignments { get; set; }
        = new List<DeviceAssignment>();

    public ICollection<DeviceEvent> DeviceEvents { get; set; }
        = new List<DeviceEvent>();
}