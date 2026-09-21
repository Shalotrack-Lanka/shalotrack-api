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

    // dealerId is optional and defaults to null (admin calls omit it and
    // keep behaving exactly as before). When a dealer calls one of these,
    // Laravel passes its own dealer's ID, and the service rejects the
    // request as not-found unless that ID matches complaint.DealerId --
    // closes the cross-dealer IDOR that existed when these methods acted
    // on any complaintId with no ownership check at all.
    Task<ApiResponse<ComplaintResponseDto>> EscalateAsync(Guid complaintId, int? dealerId = null);
    Task<ApiResponse<ComplaintResponseDto>> ResolveAsync(Guid complaintId, int? dealerId = null);
    Task<ApiResponse<ComplaintResponseDto>> CloseAsync(Guid complaintId, int? dealerId = null);
}