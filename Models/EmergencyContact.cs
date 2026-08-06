using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

public class EmergencyContact
{
    [Key]
    public Guid EmergencyContactId { get; set; }

    public Guid CustomerId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    // Optional -- "Spouse", "Parent", "Sibling", etc. Free text, not an
    // enum: the exact relationship categories weren't specified, and a
    // free-text field costs nothing to leave open rather than guessing at
    // a fixed list that might not match what the client actually wants.
    public string? Relationship { get; set; }

    public DateTime CreatedAt { get; set; }

    public Customer Customer { get; set; } = null!;
}