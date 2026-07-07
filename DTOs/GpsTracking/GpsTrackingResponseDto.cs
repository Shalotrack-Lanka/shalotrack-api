namespace ShaloTrack_API.DTOs.GpsTracking;

public class GpsTrackingResponseDto
{
    public long TrackingId { get; set; }
    public Guid VehicleId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public Guid DeviceId { get; set; }
    public string ImeiNumber { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? Altitude { get; set; }
    public decimal Speed { get; set; }
    public decimal Heading { get; set; }
    public int Satellites { get; set; }
    public decimal? GpsAccuracy { get; set; }
    public DateTime EventTime { get; set; }
}