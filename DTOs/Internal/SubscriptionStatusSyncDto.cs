namespace ShaloTrack_API.DTOs.Internal;

public class SubscriptionStatusSyncDto
{
    public List<SubscriptionStatusItemDto> Devices { get; set; } = new();
}

public class SubscriptionStatusItemDto
{
    public string Imei { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
}