using ShaloTrack_API.DTOs.Internal;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IDeviceReplacementService
{
    /// <summary>
    /// Moves the vehicle(s) on the old device's active assignment to the new device, in one
    /// transaction. Idempotent: calling it again after it has succeeded changes nothing.
    /// </summary>
    Task<ApiResponse<string>> ReplaceAsync(DeviceReplacementSyncDto dto);
}