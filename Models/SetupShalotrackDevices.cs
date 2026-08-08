using System;

namespace ShaloTrack_API.Models
{
    // Mirrors Admin's setup_shalotrack_devices table — ShaloTrack's own
    // device inventory/registry ledger (cancel reasons, dealer assignment
    // history). Deliberately kept separate from GpsDevice, which tracks
    // live tracking assignment — a different concern.
    //
    // Id matches Admin's shdevice_id directly (this table is a 1:1 mirror
    // of Admin's data, not a locally-originated entity, so reusing the same
    // integer avoids a confusing double-ID scheme).
    //
    // DealerId and DeviceTypeId are plain reference values, NOT real
    // foreign keys — the actual Dealer/DeviceType tables live in Admin's
    // own separate database. They exist here purely for display/reference.
    public class SetupShalotrackDevice
    {
        public int Id { get; set; }
        public string DeviceCategory { get; set; } = string.Empty;
        public string ImeiNumber { get; set; } = string.Empty;
        public string? SimNumber { get; set; }
        public string Status { get; set; } = "Not Activated"; // "Not Activated" | "Activated" | "Temporarily Stopped"
        public string? CancelReason { get; set; }
        public DateTime? CanceledDate { get; set; }
        public int? DealerId { get; set; }
        public int? DeviceTypeId { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}