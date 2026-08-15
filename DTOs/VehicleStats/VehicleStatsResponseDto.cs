namespace ShaloTrack_API.DTOs.VehicleStats;

public class VehicleStatsResponseDto
{
    public Guid VehicleId { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }

    public decimal TotalDistanceKm { get; set; }
    public int TotalTripCount { get; set; }

    // Sum of every Stop's duration (5+ continuous minutes stationary,
    // already computed by GpsTrackingService.GetTripsSummaryAsync) --
    // reused as-is rather than building separate idle-detection logic.
    public decimal TotalIdleMinutes { get; set; }

    // Sum of every Trip's duration -- exposed alongside TotalIdleMinutes
    // specifically so the client can show a meaningful idle-vs-driving
    // proportion, not just an idle number with nothing to compare it to.
    public decimal TotalDrivingMinutes { get; set; }

    // Duration-weighted average across trips, not a plain mean of each
    // trip's own average -- a 2-minute trip and a 2-hour trip shouldn't
    // count equally toward the overall average.
    public decimal AverageSpeed { get; set; }
    public decimal MaxSpeed { get; set; }

    // Count of real Overspeed alerts in the period, not total time spent
    // speeding -- that duration isn't actually derivable from what's
    // stored (alerts fire on the transition into speeding, not as a
    // tracked duration) without new detection logic. Reporting this
    // honestly rather than a fabricated-looking duration.
    public int OverspeedIncidentCount { get; set; }
}