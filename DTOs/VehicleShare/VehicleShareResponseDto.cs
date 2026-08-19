namespace ShaloTrack_API.DTOs.VehicleShare;

public class VehicleShareResponseDto
{
    public Guid ShareId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public Guid VehicleId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    // The other party in this relationship -- the shared-with person's
    // details when viewed by the owner, or the owner's details when
    // viewed by the shared-with person. Populated by the service
    // depending on which "side" is requesting the list.
    public Guid OtherPartyCustomerId { get; set; }
    public string OtherPartyName { get; set; } = string.Empty;
    public string OtherPartyPhoneNumber { get; set; } = string.Empty;
}