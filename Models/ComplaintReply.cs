using System.ComponentModel.DataAnnotations;
using ShaloTrack_API.Enums;

namespace ShaloTrack_API.Models;

public class ComplaintReply
{
    [Key]
    public Guid ComplaintReplyId { get; set; }

    public Guid ComplaintId { get; set; }
    public Complaint Complaint { get; set; } = null!;

    public string Message { get; set; } = string.Empty;

    public ComplaintReplyAuthorType AuthorType { get; set; }

    // Free-text display name rather than a real FK, since a reply can
    // come from a Dealer or Admin user, neither of which has a Customer
    // record in this database to join against. For a Customer-authored
    // reply this is filled in from Customer.FullName at write time.
    public string AuthorName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}