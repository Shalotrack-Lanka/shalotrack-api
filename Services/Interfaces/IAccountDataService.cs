using ShaloTrack_API.DTOs.Account;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IAccountDataService
{
    /// <summary>"Download my data": everything held about the signed-in customer (see AccountExportDto).</summary>
    Task<ApiResponse<AccountExportDto>> ExportAsync();
}