using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface ISOSService
{
    /// <summary>
    /// Triggers an SOS for a vehicle: records a real Alert (AlertType.SOS)
    /// with the vehicle's current location if available, and pushes a
    /// notification to every one of the triggering customer's own
    /// registered devices. Does NOT notify emergency contacts or shared
    /// viewers -- that depends on Vehicle Sharing, a later release. Only
    /// the vehicle's actual owner can trigger this for it, no staff bypass.
    /// </summary>
    Task<ApiResponse<string>> TriggerSOSAsync(Guid vehicleId);
}