using System;

namespace ShaloTrack_API.DTOs.SetupShalotrackDevice
{
    public class SyncSetupShalotrackDeviceDto
    {
        public int Id { get; set; }
        public string DeviceCategory { get; set; } = string.Empty;
        public string ImeiNumber { get; set; } = string.Empty;
        public string? SimNumber { get; set; }
        public string Status { get; set; } = "Not Activated";
        public string? CancelReason { get; set; }
        public DateTime? CanceledDate { get; set; }
        public int? DealerId { get; set; }
        public int? DeviceTypeId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}