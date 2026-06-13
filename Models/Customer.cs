using ShaloTrack_API.Enums;
using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

public class Customer
{
    //primary key
    [Key] 
    public Guid CustomerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NicNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ProfileImage { get; set; }
    public CustomerStatus AccountStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<Vehicle> Vehicles { get; set; }
        = new List<Vehicle>();
}