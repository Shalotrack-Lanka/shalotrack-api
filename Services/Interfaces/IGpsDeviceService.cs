using ShaloTrack_API.DTOs.GpsDevice;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IGpsDeviceService
{
    Task<ApiResponse<IReadOnlyList<GpsDeviceResponseDto>>> GetAllAsync();

    Task<ApiResponse<GpsDeviceResponseDto>> GetByIdAsync(Guid deviceId);

    Task<ApiResponse<GpsDeviceResponseDto>> CreateAsync(CreateGpsDeviceDto dto);

    Task<ApiResponse<GpsDeviceResponseDto>> UpdateAsync(
        Guid deviceId,
        UpdateGpsDeviceDto dto);

    Task<ApiResponse<string>> DeleteAsync(Guid deviceId);

    Task<ApiResponse<DeviceLookupResponseDto>> LookupByImeiAsync(string imei);
}