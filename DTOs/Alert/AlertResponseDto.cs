namespace ShaloTrack_API.DTOs.Alert;

public class AlertResponseDto
{
    public long AlertId { get; set; }
    public Guid VehicleId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime TriggeredAt { get; set; }
    public bool IsRead { get; set; }
}