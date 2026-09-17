using ShaloTrack_API.Enums;

namespace ShaloTrack_API.DTOs.Complaint;

public class ComplaintResponseDto
{
    public Guid ComplaintId { get; set; }
    public Guid VehicleId { get; set; }

    // Denormalized for display -- avoids a separate vehicle lookup just
    // to show "Complaint for [vehicle]" in a list.
    public string VehicleNumber { get; set; } = string.Empty;
    public string? Make { get; set; }
    public string? Model { get; set; }

    public ComplaintCategory Category { get; set; }
    public string Description { get; set; } = string.Empty;
    public ComplaintStatus Status { get; set; }

    public int? DealerId { get; set; }
    public string? DealerName { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? EscalatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public List<ComplaintReplyResponseDto> Replies { get; set; } = new();
}