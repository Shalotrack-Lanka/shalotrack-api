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

    // NEW -- per-vehicle alert thresholds, set by the owner. NULL means "use the default":
    // SpeedLimitKmh null = AlertDefaults.SpeedLimitKmh (80, what every vehicle used before);
    // IdleAlertMinutes null = idle alerts are OFF (opt-in, so deploying never creates new
    // notifications for existing customers). See AlertDefaults for the allowed ranges.
    public int? SpeedLimitKmh { get; set; }
    public int? IdleAlertMinutes { get; set; }

    // Per-vehicle push on/off, set by the owner (Alert settings in the app and in Fleet). Off mutes only the PUSH,
    // for the owner and every shared viewer; the alert is still saved and listed. SOS, geofence and idle alerts
    // have no switch here (SOS is never mutable; the other two have their own on/off). Defaults are true, and the
    // migration sets true on existing rows. No HasDefaultValue in the model on purpose: EF would then treat a
    // stored "false" as "not set" on insert and write the database default (true) instead.
    public bool PushPowerCut { get; set; } = true;
    public bool PushIgnition { get; set; } = true;
    public bool PushOverspeed { get; set; } = true;
    public bool PushTrackerHealth { get; set; } = true;   // low battery and offline

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Customer Customer { get; set; } = null!;
    public CurrentLocation? CurrentLocation { get; set; }

    public ICollection<DeviceAssignment> DeviceAssignments { get; set; }
        = new List<DeviceAssignment>();

    public ICollection<DeviceEvent> DeviceEvents { get; set; }
        = new List<DeviceEvent>();
}