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

    // NEW -- soft delete flag. Removing a vehicle now sets this to false
    // instead of deleting the row outright, preserving GpsTrackings/Alerts
    // history that reference this vehicle by foreign key. Defaults true so
    // every existing row remains visible after the migration runs.
    public bool IsActive { get; set; } = true;

    // NEW -- marks the one, shared demo vehicle every customer can see
    // read-only, regardless of ownership/sharing records. Structural
    // actions (edit, delete, link/unlink device, engine cut) remain
    // staff-only even for this vehicle, per direct confirmation --
    // no regular customer should be able to modify the shared demo
    // vehicle, including whoever the recorded owner happens to be.
    public bool IsDemoVehicle { get; set; } = false;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Customer Customer { get; set; } = null!;
    public CurrentLocation? CurrentLocation { get; set; }

    public ICollection<DeviceAssignment> DeviceAssignments { get; set; }
        = new List<DeviceAssignment>();

    public ICollection<DeviceEvent> DeviceEvents { get; set; }
        = new List<DeviceEvent>();
}