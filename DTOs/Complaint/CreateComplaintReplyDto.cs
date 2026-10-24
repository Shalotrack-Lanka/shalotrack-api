using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.Complaint;

public class CreateComplaintReplyDto
{
    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;
}