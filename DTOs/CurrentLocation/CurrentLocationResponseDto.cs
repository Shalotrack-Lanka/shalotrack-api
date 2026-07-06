namespace ShaloTrack_API.DTOs.CurrentLocation;

public class CurrentLocationResponseDto
{
    public Guid VehicleId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public string ImeiNumber { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal Speed { get; set; }
    public decimal Heading { get; set; }
    public bool IgnitionStatus { get; set; }
    public bool MovementStatus { get; set; }
    public DateTime LastUpdate { get; set; }
}