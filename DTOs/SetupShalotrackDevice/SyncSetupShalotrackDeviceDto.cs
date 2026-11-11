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
        // Optional so payloads from an older Admin build still deserialize.
        // null = Admin did not say (leave BoundAt untouched);
        // true = bound to a customer vehicle; false = not bound.
        public bool? IsBound { get; set; }
        public DateTime? BoundAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}