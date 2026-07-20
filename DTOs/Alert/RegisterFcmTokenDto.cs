using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.Alert;

public class RegisterFcmTokenDto
{
    [Required]
    public string FcmToken { get; set; } = string.Empty;

    public string Platform { get; set; } = "android";
}