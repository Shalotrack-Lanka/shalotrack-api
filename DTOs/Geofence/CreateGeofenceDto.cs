namespace ShaloTrack_API.DTOs.Geofence;

public class CreateGeofenceDto
{
    // Null applies this geofence to every one of the customer's vehicles;
    // set to a specific vehicle to scope it to just that one.
    public Guid? VehicleId { get; set; }

    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int RadiusMeters { get; set; }

    // Both default to true (per direct confirmation) if the client omits
    // them entirely -- handled in the service, not here, since a DTO
    // property's own C# default (false for bool) would silently override
    // that intended default the moment the DTO gets bound at all.
    public bool? AlertOnEnter { get; set; }
    public bool? AlertOnExit { get; set; }
}