namespace ShaloTrack_API.DTOs.GpsTracking;

public class TripsReportResponseDto
{
    public Guid VehicleId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TripCount { get; set; }
    public int StopCount { get; set; }
    public List<TripSummaryDto> Trips { get; set; } = new();
    public List<StopSummaryDto> Stops { get; set; } = new();
}