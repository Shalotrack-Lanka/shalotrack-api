namespace ShaloTrack_API.DTOs.Alert;

/// <summary>
/// NEW -- report generation feature. Backs the mobile "Alert Report" card:
/// unlike GET api/Alerts (paged, most-recent-first, no date filter -- built
/// for the notification-feed screen), this is a bounded [from, to] window
/// with a per-type count summary as well as the full list, for a report the
/// user picked a specific date range for.
/// </summary>
public class AlertReportResponseDto
{
    public Guid VehicleId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int TotalCount { get; set; }

    // Every AlertType with at least one occurrence in the window, keyed by
    // its string name (matches AlertResponseDto.AlertType's own format, so
    // the client can reuse one enum->label mapping for both).
    public Dictionary<string, int> CountsByType { get; set; } = new();

    public List<AlertResponseDto> Alerts { get; set; } = new();
}