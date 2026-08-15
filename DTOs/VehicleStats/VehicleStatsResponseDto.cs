namespace ShaloTrack_API.DTOs.VehicleStats;

public class VehicleStatsResponseDto
{
    public Guid VehicleId { get; set; }
    public string Period { get; set; } = string.Empty; // "today" | "week" | "month" | "all"
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }

    public decimal TotalDistanceKm { get; set; }
    public int TotalTripCount { get; set; }

    // NEW -- Letstrack shows Stops as its own metric card, not folded into
    // idle time.
    public int TotalStopCount { get; set; }

    // Sum of every Stop's duration (5+ continuous minutes stationary,
    // already computed by GpsTrackingService.GetTripsSummaryAsync) --
    // reused as-is rather than building separate idle-detection logic.
    public decimal TotalIdleMinutes { get; set; }

    // Sum of every Trip's duration.
    public decimal TotalDrivingMinutes { get; set; }

    // NEW -- matches Letstrack's "Ignition On" card. Defined as driving
    // time + idle time (the whole span from ignition-on to ignition-off
    // across all episodes in the period), not a separately-detected
    // value -- deliberately reuses the two totals already computed above
    // rather than adding new ignition-duration detection logic.
    public decimal TotalIgnitionOnMinutes { get; set; }

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

    // NEW -- one entry per calendar day in the period (Sri Lanka local
    // time, not UTC -- a trip near midnight UTC would otherwise land on
    // the wrong day for someone actually reading the chart), for the
    // per-day bar chart.
    public List<DailyStatDto> DailyBreakdown { get; set; } = new();
}