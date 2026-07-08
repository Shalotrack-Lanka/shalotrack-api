using ShaloTrack_API.DTOs.DeviceEvent;
using ShaloTrack_API.Filters;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;
using System.Net;

namespace ShaloTrack_API.Services.Implementations;

public class DeviceEventService : IDeviceEventService
{
    private readonly IDeviceEventRepository _repository;

    public DeviceEventService(
        IDeviceEventRepository repository)
    {
        _repository = repository;
    }

    public async Task<ApiResponse<List<DeviceEventResponseDto>>> GetAsync(
        DeviceEventFilter filter)
    {
        var deviceevents = await _repository.GetAsync(filter);

        return ApiResponse<List<DeviceEventResponseDto>>.Ok(deviceevents);
    }

    public async Task<ApiResponse<DeviceEventResponseDto>> GetByIdAsync(
        long eventId)
    {
        var deviceEvent = await _repository.GetByIdAsync(eventId);

        if (deviceEvent == null)
        {
            return ApiResponse<DeviceEventResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Device event not found.");
        }

        return ApiResponse<DeviceEventResponseDto>.Ok(deviceEvent);
    }
}