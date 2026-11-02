using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.Customer;

public class CreateCustomerDto
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    // IGNORED by the server: the profile always gets the phone number Firebase holds for the caller
    // (the one our OTP verified, see CustomerService.CreateAsync). Kept optional so existing clients that
    // still send it keep working and new clients are not forced to.
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    // Sri Lankan NIC: old format 9 digits + V/X (e.g. 861234567V) or new format 12 digits (e.g. 198612345678).
    // Surrounding spaces are tolerated here and removed by CustomerService before the uniqueness check.
    [Required]
    [MaxLength(20)]
    [RegularExpression(@"^\s*([0-9]{9}[VvXx]|[0-9]{12})\s*$", ErrorMessage = "Enter a valid NIC number (e.g. 861234567V or 198612345678).")]
    public string NicNumber { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }
}