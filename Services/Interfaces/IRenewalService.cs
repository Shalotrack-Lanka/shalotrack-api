using ShaloTrack_API.DTOs.Renewals;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IRenewalService
{
    // ---- customer (owner of the vehicle only) ----
    Task<ApiResponse<RenewalResponseDto>> CreateAsync(CreateRenewalDto dto);
    Task<ApiResponse<RenewalResponseDto>> UploadSlipAsync(Guid renewalRequestId, IFormFile file);
    Task<ApiResponse<IReadOnlyList<RenewalResponseDto>>> GetMineAsync();
    Task<ApiResponse<RenewalResponseDto>> CancelAsync(Guid renewalRequestId);

    // ---- staff / admin portal (internal routes) ----
    Task<ApiResponse<IReadOnlyList<InternalRenewalDto>>> GetForAdminAsync(string? status, CancellationToken cancellationToken);
    Task<(byte[] Content, string ContentType)?> GetSlipForAdminAsync(Guid renewalRequestId, CancellationToken cancellationToken);
    Task<ApiResponse<InternalRenewalDto>> DecideAsync(Guid renewalRequestId, DecideRenewalDto dto);
}