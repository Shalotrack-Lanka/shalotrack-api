using ShaloTrack_API.DTOs.WeeklySummary;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IWeeklySummarySettingsService
{
    Task<ApiResponse<WeeklySummarySettingsDto>> GetAsync();
    Task<ApiResponse<WeeklySummarySettingsDto>> UpdateAsync(WeeklySummarySettingsDto dto);
}