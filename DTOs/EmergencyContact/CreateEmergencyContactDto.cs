using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.EmergencyContact;

// Validated here because the Android app calls the API directly: the web portal's own checks are not enough.
// Limits match the Fleet portal (EmergencyContactController) so both clients accept the same input.
public class CreateEmergencyContactDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [RegularExpression(@"^\+?[0-9][0-9\s().-]{6,19}$", ErrorMessage = "Enter a valid phone number.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Relationship { get; set; }
}