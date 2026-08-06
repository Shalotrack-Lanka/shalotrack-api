namespace ShaloTrack_API.DTOs.EmergencyContact;

public class EmergencyContactResponseDto
{
    public Guid EmergencyContactId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public DateTime CreatedAt { get; set; }
}