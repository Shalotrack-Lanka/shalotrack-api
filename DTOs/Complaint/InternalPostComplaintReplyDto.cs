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
}