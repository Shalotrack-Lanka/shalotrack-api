using ShaloTrack_API.DTOs.DeviceEvent;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IDeviceEventRepository
{
    Task<List<DeviceEventResponseDto>> GetAsync(
        DeviceEventFilter filter);

    Task<DeviceEventResponseDto?> GetByIdAsync(
        long eventId);

    // NEW: this repository was read-only before -- every existing row
    // gets written by the gateway through a separate path entirely (see
    // gateway's own services/event_service.py, a direct DB insert, not
    // through this API). This is the first write path into DeviceEvents
    // from the C# API side, added specifically for SOS.
    Task AddAsync(DeviceEvent deviceEvent);
}