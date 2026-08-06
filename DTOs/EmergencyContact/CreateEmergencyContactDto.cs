namespace ShaloTrack_API.DTOs.EmergencyContact;

public class CreateEmergencyContactDto
{
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Relationship { get; set; }
}