namespace ShaloTrack_API.DTOs.GpsTracking;

public class TripSummaryDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal StartLatitude { get; set; }
    public decimal StartLongitude { get; set; }
    public decimal EndLatitude { get; set; }
    public decimal EndLongitude { get; set; }
    public decimal DurationMinutes { get; set; }
    public decimal DistanceKm { get; set; }

    // NEW -- for the Speed report tab.
    public decimal MaxSpeed { get; set; }
    public decimal AvgSpeed { get; set; }

    public bool InProgress { get; set; }
}