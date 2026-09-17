using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class ComplaintRepository : IComplaintRepository
{
    private readonly ShaloTrackDbContext _context;

    public ComplaintRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Complaint complaint)
    {
        await _context.Complaints.AddAsync(complaint);
    }

    public async Task<Complaint?> GetByIdAsync(Guid complaintId)
    {
        return await _context.Complaints
            .Include(c => c.Vehicle)
            .Include(c => c.Replies.OrderBy(r => r.CreatedAt))
            .FirstOrDefaultAsync(c => c.ComplaintId == complaintId);
    }

    public async Task<List<Complaint>> GetByCustomerAsync(Guid customerId)
    {
        return await _context.Complaints
            .AsNoTracking()
            .Include(c => c.Vehicle)
            .Include(c => c.Replies.OrderBy(r => r.CreatedAt))
            .Where(c => c.CustomerId == customerId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Complaint>> GetByDealerAsync(int dealerId)
    {
        return await _context.Complaints
            .AsNoTracking()
            .Include(c => c.Vehicle)
            .Include(c => c.Customer)
            .Include(c => c.Replies.OrderBy(r => r.CreatedAt))
            .Where(c => c.DealerId == dealerId && c.Status == ComplaintStatus.WithDealer)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Complaint>> GetForAdminAsync()
    {
        return await _context.Complaints
            .AsNoTracking()
            .Include(c => c.Vehicle)
            .Include(c => c.Customer)
            .Include(c => c.Replies.OrderBy(r => r.CreatedAt))
            .Where(c => c.Status == ComplaintStatus.WithAdmin)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public void Update(Complaint complaint)
    {
        _context.Complaints.Update(complaint);
    }

    public async Task AddReplyAsync(ComplaintReply reply)
    {
        await _context.ComplaintReplies.AddAsync(reply);
    }
}