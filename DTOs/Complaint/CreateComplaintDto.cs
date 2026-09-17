using ShaloTrack_API.Enums;

namespace ShaloTrack_API.DTOs.Complaint;

public class CreateComplaintDto
{
    public Guid VehicleId { get; set; }
    public ComplaintCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
}