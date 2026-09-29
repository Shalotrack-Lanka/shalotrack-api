using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

public class DeviceSubscriptionStatus
{
    [Key]
    public string ImeiNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}