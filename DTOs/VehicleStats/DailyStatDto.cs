namespace ShaloTrack_API.DTOs.VehicleStats;

/// <summary>
/// One calendar day's aggregated stats (Sri Lanka local time), for the
/// per-day bar chart.
/// </summary>
public class DailyStatDto
{
    public DateTime Date { get; set; }
    public decimal DistanceKm { get; set; }
    public decimal AverageSpeed { get; set; }
    public decimal MaxSpeed { get; set; }
    public int TripCount { get; set; }
    public int StopCount { get; set; }
    public decimal IgnitionOnMinutes { get; set; }
}