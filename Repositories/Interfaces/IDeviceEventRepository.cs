using ShaloTrack_API.DTOs.DeviceEvent;
using ShaloTrack_API.Filters;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IDeviceEventRepository
{
    Task<List<DeviceEventResponseDto>> GetAsync(
        DeviceEventFilter filter);

    Task<DeviceEventResponseDto?> GetByIdAsync(
        long eventId);
}