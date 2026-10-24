using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.Alert;

public class RegisterFcmTokenDto
{
    [Required]
    [MaxLength(4096)]
    public string FcmToken { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Platform { get; set; } = "android";
}