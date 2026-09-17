using ShaloTrack_API.Enums;

namespace ShaloTrack_API.DTOs.Complaint;

public class ComplaintReplyResponseDto
{
    public Guid ComplaintReplyId { get; set; }
    public string Message { get; set; } = string.Empty;
    public ComplaintReplyAuthorType AuthorType { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}