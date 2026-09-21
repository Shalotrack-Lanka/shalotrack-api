using ShaloTrack_API.Enums;

namespace ShaloTrack_API.DTOs.Complaint;

// Posted by Admin (dealer or admin user replying), not by the customer's
// own app. AuthorType/AuthorName are supplied explicitly since this
// caller is authenticated via the internal sync key, not a Firebase
// token tied to a Customer record.
public class InternalPostComplaintReplyDto
{
    public string Message { get; set; } = string.Empty;
    public ComplaintReplyAuthorType AuthorType { get; set; }
    public string AuthorName { get; set; } = string.Empty;

    // NEW -- when this reply comes from a dealer, Laravel supplies its own
    // dealer's ID here so the service can verify the complaint actually
    // belongs to that dealer before applying anything. Admin's own calls
    // never set this (stays null), so admin replies are unaffected.
    public int? DealerId { get; set; }
}