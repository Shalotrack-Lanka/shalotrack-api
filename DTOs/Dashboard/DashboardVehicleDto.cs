namespace ShaloTrack_API.DTOs.Dashboard;

public class DashboardVehicleDto
{
    public Guid VehicleId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public Guid? DeviceId { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal Speed { get; set; }
    public decimal Heading { get; set; }
    public bool Online { get; set; } 
    public bool Ignition { get; set; } 
    public DateTime? LastUpdate { get; set; }
}