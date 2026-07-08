using ShaloTrack_API.DTOs.DeviceEvent;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IDeviceEventService
{
    Task<ApiResponse<List<DeviceEventResponseDto>>> GetAsync(
        DeviceEventFilter filter);

    Task<ApiResponse<DeviceEventResponseDto>> GetByIdAsync(
        long eventId);
}