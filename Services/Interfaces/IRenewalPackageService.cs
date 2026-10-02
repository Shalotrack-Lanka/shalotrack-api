using ShaloTrack_API.DTOs.Renewals;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IRenewalPackageService
{
    /// <summary>Customer: the packages that can be bought right now, cheapest first.</summary>
    Task<ApiResponse<IReadOnlyList<RenewalPackageDto>>> GetOfferedAsync(CancellationToken cancellationToken);

    /// <summary>Admin portal (internal route): replace the stored copy of the price master.</summary>
    Task<ApiResponse<int>> SyncAsync(SyncRenewalPackagesDto dto, CancellationToken cancellationToken);
}