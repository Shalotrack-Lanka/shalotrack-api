using ShaloTrack_API.DTOs.Complaint;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IComplaintService
{
    // ---- Customer-facing (Android, via Firebase-authenticated calls) ----

    Task<ApiResponse<ComplaintResponseDto>> CreateAsync(CreateComplaintDto dto);
    Task<ApiResponse<IReadOnlyList<ComplaintResponseDto>>> GetMineAsync();
    Task<ApiResponse<ComplaintResponseDto>> GetByIdAsync(Guid complaintId);
    Task<ApiResponse<ComplaintResponseDto>> AddCustomerReplyAsync(Guid complaintId, CreateComplaintReplyDto dto);

    // ---- Internal (Laravel, via the shared sync-key secret) ----

    Task<ApiResponse<IReadOnlyList<ComplaintResponseDto>>> GetByDealerAsync(int dealerId);
    Task<ApiResponse<IReadOnlyList<ComplaintResponseDto>>> GetForAdminAsync();
    Task<ApiResponse<ComplaintResponseDto>> AddInternalReplyAsync(Guid complaintId, InternalPostComplaintReplyDto dto);
    Task<ApiResponse<ComplaintResponseDto>> EscalateAsync(Guid complaintId);
    Task<ApiResponse<ComplaintResponseDto>> ResolveAsync(Guid complaintId);
    Task<ApiResponse<ComplaintResponseDto>> CloseAsync(Guid complaintId);
}