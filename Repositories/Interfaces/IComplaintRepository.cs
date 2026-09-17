using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IComplaintRepository
{
    Task AddAsync(Complaint complaint);

    /// <summary>Includes Vehicle and Replies for full DTO mapping.</summary>
    Task<Complaint?> GetByIdAsync(Guid complaintId);

    /// <summary>The customer's own complaints, newest first.</summary>
    Task<List<Complaint>> GetByCustomerAsync(Guid customerId);

    /// <summary>Every complaint currently routed to this dealer (WithDealer status only).</summary>
    Task<List<Complaint>> GetByDealerAsync(int dealerId);

    /// <summary>Every complaint currently with admin (WithAdmin status only) -- no dealer was matched, or one escalated it.</summary>
    Task<List<Complaint>> GetForAdminAsync();

    void Update(Complaint complaint);

    Task AddReplyAsync(ComplaintReply reply);
}