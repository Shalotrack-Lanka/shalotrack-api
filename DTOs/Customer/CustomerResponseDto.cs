using ShaloTrack_API.Enums;

namespace ShaloTrack_API.DTOs.Customer;

public class CustomerResponseDto
{
    public Guid CustomerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NicNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ProfileImage { get; set; }
    public CustomerStatus AccountStatus { get; set; }
    public int VehicleCount { get; set; }
}