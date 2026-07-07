namespace ShaloTrack_API.Filters;

public class GpsTrackingFilter
{
    public Guid? VehicleId { get; set; }
    public Guid? DeviceId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 100;
}