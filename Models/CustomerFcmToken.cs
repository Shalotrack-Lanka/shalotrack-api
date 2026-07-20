using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

/// <summary>
/// A customer's FCM device token. A customer can have multiple (phone + tablet,
/// or after reinstalling), so this is a separate table, not a column on Customer.
/// </summary>
public class CustomerFcmToken
{
    [Key]
    public long TokenId { get; set; }

    public Guid CustomerId { get; set; }
    public string FcmToken { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";
    public DateTime UpdatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
}