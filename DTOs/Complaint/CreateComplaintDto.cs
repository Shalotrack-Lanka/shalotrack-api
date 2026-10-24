using System.ComponentModel.DataAnnotations;
using ShaloTrack_API.Enums;

namespace ShaloTrack_API.DTOs.Complaint;

public class CreateComplaintDto
{
    public Guid VehicleId { get; set; }

    // An enum binds ANY integer from JSON (e.g. 99): this rejects values that are not a defined category.
    [EnumDataType(typeof(ComplaintCategory))]
    public ComplaintCategory Category { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;
}