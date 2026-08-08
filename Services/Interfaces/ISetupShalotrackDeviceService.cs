using ShaloTrack_API.DTOs.SetupShalotrackDevice;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface ISetupShalotrackDeviceService
{
    Task<ApiResponse<string>> UpsertAsync(SyncSetupShalotrackDeviceDto dto);
}