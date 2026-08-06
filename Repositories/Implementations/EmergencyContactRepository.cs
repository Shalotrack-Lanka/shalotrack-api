using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class EmergencyContactRepository : IEmergencyContactRepository
{
    private readonly ShaloTrackDbContext _context;

    public EmergencyContactRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<EmergencyContact>> GetByCustomerAsync(Guid customerId)
    {
        return await _context.EmergencyContacts
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<EmergencyContact?> GetByIdAsync(Guid emergencyContactId)
    {
        return await _context.EmergencyContacts
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.EmergencyContactId == emergencyContactId);
    }

    public async Task AddAsync(EmergencyContact contact)
    {
        await _context.EmergencyContacts.AddAsync(contact);
    }

    public void Remove(EmergencyContact contact)
    {
        _context.EmergencyContacts.Remove(contact);
    }
}