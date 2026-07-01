using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.Customer;

public class UpdateCustomerDto
{
    [Required]
    [MaxLength(150)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [Phone]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }

    public string? ProfileImage { get; set; }
}