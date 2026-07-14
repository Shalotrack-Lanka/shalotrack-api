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

    // NEW -- actual route distance (sum of point-to-point distance along the trip),
    // not straight-line start-to-end displacement.
    public decimal DistanceKm { get; set; }

    /// <summary>True if the vehicle was still moving when the requested time window
    /// ended (trip wasn't actually finished, just cut off by the report's end time).</summary>
    public bool InProgress { get; set; }
}