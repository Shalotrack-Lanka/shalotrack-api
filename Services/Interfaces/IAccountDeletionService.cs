using ShaloTrack_API.DTOs.Account;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IAccountDeletionService
{
    Task<ApiResponse<DeletionStatusDto>> GetStatusAsync();

    /// <summary>Locks the account and schedules erasure 30 days out. Needs the confirm word and a recent sign-in.</summary>
    Task<ApiResponse<DeletionStatusDto>> RequestAsync(DeleteAccountRequestDto dto);

    /// <summary>Withdraws a pending request (only possible before the purge runs).</summary>
    Task<ApiResponse<DeletionStatusDto>> CancelAsync();
}