namespace ShaloTrack_API.DTOs.GpsTracking;

/// <summary>An individual stop -- where the vehicle sat stationary for 5+ minutes.</summary>
public class StopSummaryDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal DurationMinutes { get; set; }

    /// <summary>True if the vehicle was still stationary when the requested window ended.</summary>
    public bool InProgress { get; set; }
}