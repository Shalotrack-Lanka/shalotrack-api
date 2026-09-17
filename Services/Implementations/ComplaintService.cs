using System.Net;
using System.Net.Http.Json;
using ShaloTrack_API.Auth;
using ShaloTrack_API.DTOs.Complaint;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class ComplaintService : IComplaintService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IPushNotificationService _pushNotificationService;
    private readonly HttpClient _adminPortalClient;
    private readonly string _syncKey;
    private readonly ILogger<ComplaintService> _logger;

    public ComplaintService(
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IPushNotificationService pushNotificationService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<ComplaintService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _pushNotificationService = pushNotificationService;
        _adminPortalClient = httpClientFactory.CreateClient("AdminPortal");
        _syncKey = configuration["AdminSync:Key"] ?? string.Empty;
        _logger = logger;
    }

    // ---- Customer-facing ----

    public async Task<ApiResponse<ComplaintResponseDto>> CreateAsync(CreateComplaintDto dto)
    {
        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(_currentUser.FirebaseUid ?? string.Empty);
        if (customer is null)
        {
            return ApiResponse<ComplaintResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.",
                "A verified account is required to file a complaint.");
        }

        // Strict ownership -- not the owner-or-shared-or-demo pattern used
        // for read access elsewhere. A complaint is about a specific,
        // real device fault; it doesn't make sense for a shared viewer
        // or the demo vehicle, since neither is the customer's own
        // device to have a fault at all.
        var vehicle = await _unitOfWork.Vehicles.GetByIdAsync(dto.VehicleId);
        if (vehicle is null || vehicle.CustomerId != customer.CustomerId)
        {
            return ApiResponse<ComplaintResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Vehicle not found.",
                $"No vehicle exists with ID '{dto.VehicleId}' for this account.");
        }

        var dealerLookup = await LookupDealerAsync(customer.Email, customer.PhoneNumber);

        var complaint = new Complaint
        {
            ComplaintId = Guid.NewGuid(),
            CustomerId = customer.CustomerId,
            VehicleId = vehicle.VehicleId,
            Category = dto.Category,
            Description = dto.Description,
            Status = dealerLookup?.DealerId is not null ? ComplaintStatus.WithDealer : ComplaintStatus.WithAdmin,
            DealerId = dealerLookup?.DealerId,
            DealerName = dealerLookup?.DealerName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Complaints.AddAsync(complaint);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<ComplaintResponseDto>.Ok(ToDto(complaint, vehicle), "Complaint filed successfully.");
    }

    public async Task<ApiResponse<IReadOnlyList<ComplaintResponseDto>>> GetMineAsync()
    {
        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(_currentUser.FirebaseUid ?? string.Empty);
        if (customer is null)
        {
            return ApiResponse<IReadOnlyList<ComplaintResponseDto>>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "");
        }

        var complaints = await _unitOfWork.Complaints.GetByCustomerAsync(customer.CustomerId);
        return ApiResponse<IReadOnlyList<ComplaintResponseDto>>.Ok(
            complaints.Select(c => ToDto(c, c.Vehicle)).ToList(),
            "Complaints retrieved successfully.");
    }

    public async Task<ApiResponse<ComplaintResponseDto>> GetByIdAsync(Guid complaintId)
    {
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        if (complaint is null)
        {
            return ApiResponse<ComplaintResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Complaint not found.", "");
        }

        if (!_currentUser.IsStaff)
        {
            var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(_currentUser.FirebaseUid ?? string.Empty);
            if (customer is null || complaint.CustomerId != customer.CustomerId)
            {
                // Same not-found response as missing, not 403 -- doesn't
                // confirm existence to someone else's complaint.
                return ApiResponse<ComplaintResponseDto>.Fail(
                    (int)HttpStatusCode.NotFound, "Complaint not found.", "");
            }
        }

        return ApiResponse<ComplaintResponseDto>.Ok(ToDto(complaint, complaint.Vehicle), "Complaint retrieved successfully.");
    }

    public async Task<ApiResponse<ComplaintResponseDto>> AddCustomerReplyAsync(Guid complaintId, CreateComplaintReplyDto dto)
    {
        var customer = await _unitOfWork.Customers.GetByFirebaseUidAsync(_currentUser.FirebaseUid ?? string.Empty);
        if (customer is null)
        {
            return ApiResponse<ComplaintResponseDto>.Fail(
                (int)HttpStatusCode.Unauthorized, "Authentication required.", "");
        }

        var complaint = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        if (complaint is null || complaint.CustomerId != customer.CustomerId)
        {
            return ApiResponse<ComplaintResponseDto>.Fail(
                (int)HttpStatusCode.NotFound, "Complaint not found.", "");
        }

        var reply = new ComplaintReply
        {
            ComplaintReplyId = Guid.NewGuid(),
            ComplaintId = complaint.ComplaintId,
            Message = dto.Message,
            AuthorType = ComplaintReplyAuthorType.Customer,
            AuthorName = customer.FullName,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Complaints.AddReplyAsync(reply);
        complaint.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Complaints.Update(complaint);
        await _unitOfWork.SaveChangesAsync();

        // No push here -- the customer replying to their own complaint
        // doesn't need to be notified of their own action. Dealer/admin
        // side surfaces new customer replies within the portal itself,
        // not via push (out of scope for this session's Android/API work).

        var refreshed = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        return ApiResponse<ComplaintResponseDto>.Ok(ToDto(refreshed!, refreshed!.Vehicle), "Reply added.");
    }

    // ---- Internal (Laravel) ----

    public async Task<ApiResponse<IReadOnlyList<ComplaintResponseDto>>> GetByDealerAsync(int dealerId)
    {
        var complaints = await _unitOfWork.Complaints.GetByDealerAsync(dealerId);
        return ApiResponse<IReadOnlyList<ComplaintResponseDto>>.Ok(
            complaints.Select(c => ToDto(c, c.Vehicle)).ToList(),
            "Complaints retrieved successfully.");
    }

    public async Task<ApiResponse<IReadOnlyList<ComplaintResponseDto>>> GetForAdminAsync()
    {
        var complaints = await _unitOfWork.Complaints.GetForAdminAsync();
        return ApiResponse<IReadOnlyList<ComplaintResponseDto>>.Ok(
            complaints.Select(c => ToDto(c, c.Vehicle)).ToList(),
            "Complaints retrieved successfully.");
    }

    public async Task<ApiResponse<ComplaintResponseDto>> AddInternalReplyAsync(Guid complaintId, InternalPostComplaintReplyDto dto)
    {
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        if (complaint is null)
        {
            return ApiResponse<ComplaintResponseDto>.Fail((int)HttpStatusCode.NotFound, "Complaint not found.", "");
        }

        var reply = new ComplaintReply
        {
            ComplaintReplyId = Guid.NewGuid(),
            ComplaintId = complaint.ComplaintId,
            Message = dto.Message,
            AuthorType = dto.AuthorType,
            AuthorName = dto.AuthorName,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Complaints.AddReplyAsync(reply);
        complaint.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Complaints.Update(complaint);
        await _unitOfWork.SaveChangesAsync();

        var authorLabel = dto.AuthorType == ComplaintReplyAuthorType.Dealer ? "your dealer" : "ShaloTrack support";
        await _pushNotificationService.SendAlertPushAsync(
            complaint.CustomerId,
            "New reply on your complaint",
            $"{authorLabel} replied: {Truncate(dto.Message, 100)}");

        var refreshed = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        return ApiResponse<ComplaintResponseDto>.Ok(ToDto(refreshed!, refreshed!.Vehicle), "Reply added.");
    }

    public async Task<ApiResponse<ComplaintResponseDto>> EscalateAsync(Guid complaintId)
    {
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        if (complaint is null)
        {
            return ApiResponse<ComplaintResponseDto>.Fail((int)HttpStatusCode.NotFound, "Complaint not found.", "");
        }

        if (complaint.Status != ComplaintStatus.WithDealer)
        {
            return ApiResponse<ComplaintResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest, "Cannot escalate.",
                "Only a complaint currently with a dealer can be escalated to admin.");
        }

        complaint.Status = ComplaintStatus.WithAdmin;
        complaint.EscalatedAt = DateTime.UtcNow;
        complaint.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Complaints.Update(complaint);
        await _unitOfWork.SaveChangesAsync();

        await _pushNotificationService.SendAlertPushAsync(
            complaint.CustomerId,
            "Complaint escalated",
            "Your complaint has been transferred to ShaloTrack support for further help.");

        return ApiResponse<ComplaintResponseDto>.Ok(ToDto(complaint, complaint.Vehicle), "Complaint escalated.");
    }

    public async Task<ApiResponse<ComplaintResponseDto>> ResolveAsync(Guid complaintId)
    {
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        if (complaint is null)
        {
            return ApiResponse<ComplaintResponseDto>.Fail((int)HttpStatusCode.NotFound, "Complaint not found.", "");
        }

        complaint.Status = ComplaintStatus.Resolved;
        complaint.ResolvedAt = DateTime.UtcNow;
        complaint.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Complaints.Update(complaint);
        await _unitOfWork.SaveChangesAsync();

        await _pushNotificationService.SendAlertPushAsync(
            complaint.CustomerId,
            "Complaint resolved",
            "Your complaint has been marked as resolved.");

        return ApiResponse<ComplaintResponseDto>.Ok(ToDto(complaint, complaint.Vehicle), "Complaint resolved.");
    }

    public async Task<ApiResponse<ComplaintResponseDto>> CloseAsync(Guid complaintId)
    {
        var complaint = await _unitOfWork.Complaints.GetByIdAsync(complaintId);
        if (complaint is null)
        {
            return ApiResponse<ComplaintResponseDto>.Fail((int)HttpStatusCode.NotFound, "Complaint not found.", "");
        }

        complaint.Status = ComplaintStatus.Closed;
        complaint.UpdatedAt = DateTime.UtcNow;
        _unitOfWork.Complaints.Update(complaint);
        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<ComplaintResponseDto>.Ok(ToDto(complaint, complaint.Vehicle), "Complaint closed.");
    }

    // ---- Helpers ----

    // Real-time call to Admin's dealer-matching endpoint at filing time.
    // Returns null on ANY failure (timeout, non-200, network error,
    // malformed response) -- a failed lookup falls back to "no dealer
    // found" (WithAdmin), which is a safe, valid, already-intended path
    // for a customer with no dealer. Never lets Admin being unreachable
    // block filing a complaint.
    private async Task<DealerLookupResultDto?> LookupDealerAsync(string email, string phoneNumber)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/internal/dealer-lookup?email={Uri.EscapeDataString(email)}&phone={Uri.EscapeDataString(phoneNumber)}");
            request.Headers.Add("X-Admin-Sync-Key", _syncKey);

            var response = await _adminPortalClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Dealer lookup failed, status {StatusCode}. Falling back to no-dealer.", response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<DealerLookupResultDto>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Dealer lookup call to Admin portal failed. Falling back to no-dealer.");
            return null;
        }
    }

    private static string Truncate(string s, int maxLength) =>
        s.Length <= maxLength ? s : s[..maxLength] + "...";

    private static ComplaintResponseDto ToDto(Complaint c, Vehicle vehicle) => new()
    {
        ComplaintId = c.ComplaintId,
        VehicleId = c.VehicleId,
        VehicleNumber = vehicle.VehicleNumber,
        Make = vehicle.Make,
        Model = vehicle.Model,
        Category = c.Category,
        Description = c.Description,
        Status = c.Status,
        DealerId = c.DealerId,
        DealerName = c.DealerName,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
        EscalatedAt = c.EscalatedAt,
        ResolvedAt = c.ResolvedAt,
        Replies = c.Replies.Select(r => new ComplaintReplyResponseDto
        {
            ComplaintReplyId = r.ComplaintReplyId,
            Message = r.Message,
            AuthorType = r.AuthorType,
            AuthorName = r.AuthorName,
            CreatedAt = r.CreatedAt
        }).ToList()
    };
}