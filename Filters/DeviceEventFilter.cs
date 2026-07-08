namespace ShaloTrack_API.Filters;

public class DeviceEventFilter
{
    public Guid? DeviceId { get; set; }
    public Guid? VehicleId { get; set; }
    public string? EventType { get; set; }
    public short? Severity { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}