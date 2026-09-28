using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IComplaintRepository
{
    Task AddAsync(Complaint complaint);

    /// <summary>Includes Vehicle and Replies for full DTO mapping.</summary>
    Task<Complaint?> GetByIdAsync(Guid complaintId);

    /// <summary>The customer's own complaints, newest first.</summary>
    Task<List<Complaint>> GetByCustomerAsync(Guid customerId);

    /// <summary>Every complaint currently routed to this dealer, all statuses (see ComplaintRepository for why).</summary>
    Task<List<Complaint>> GetByDealerAsync(int dealerId);

    /// <summary>Every complaint currently with admin (WithAdmin status only) -- no dealer was matched, or one escalated it.</summary>
    Task<List<Complaint>> GetForAdminAsync();

    /// <summary>
    /// NEW -- every complaint the admin (or a dealer, on admin's behalf)
    /// has marked Resolved, newest-resolved first. Backs the admin
    /// portal's "Resolved Complaints" tab. Deliberately Resolved only,
    /// not Closed -- Closed is a separate terminal state (a complaint
    /// that was closed without necessarily going through Resolved) and
    /// the admin portal's resolved view has never shown it. If Closed
    /// complaints need their own view later, add a sibling method rather
    /// than conflating the two here.
    /// </summary>
    Task<List<Complaint>> GetResolvedForAdminAsync();

    void Update(Complaint complaint);

    Task AddReplyAsync(ComplaintReply reply);
}