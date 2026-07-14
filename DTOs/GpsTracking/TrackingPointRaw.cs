namespace ShaloTrack_API.DTOs.GpsTracking;

/// <summary>
/// Lightweight projection used only for server-side trip/stop calculation.
/// Never returned directly to a client — see TripsReportResponseDto for that.
/// </summary>
public class TrackingPointRaw
{
    public DateTime EventTime { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal Speed { get; set; }
}