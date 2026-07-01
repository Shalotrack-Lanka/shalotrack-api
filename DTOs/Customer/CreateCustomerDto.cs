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

    [Required]
    [Phone]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string NicNumber { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }
}