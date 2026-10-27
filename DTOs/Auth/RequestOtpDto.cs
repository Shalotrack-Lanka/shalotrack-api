using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.Auth;

public class RequestOtpDto
{
    /// <summary>Any common Sri Lankan format: "0771234567", "94771234567", "+94771234567". Normalized server-side.</summary>
    [Required]
    [StringLength(20, MinimumLength = 9)]
    public string PhoneNumber { get; set; } = string.Empty;
}
