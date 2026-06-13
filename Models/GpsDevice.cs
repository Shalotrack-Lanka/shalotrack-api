using ShaloTrack_API.Enums;
using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

public class GpsDevice
{
    //primary key
    [Key]
    public Guid DeviceId { get; set; }
    public string ImeiNumber { get; set; } = string.Empty;
    public string? SimNumber { get; set; }
    public string DeviceModel { get; set; } = string.Empty;
    public string ProtocolType { get; set; } = string.Empty;
    public string? NetworkProvider { get; set; }
    public string? FirmwareVersion { get; set; }
    public ActivationStatus ActivationStatus { get; set; }
    public DateTime? WarrantyExpiryDate { get; set; }
    public DateTime? InstalledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<DeviceAssignment> DeviceAssignments { get; set; }
        = new List<DeviceAssignment>();

    
}