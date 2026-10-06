using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class VehicleReminderRepository : IVehicleReminderRepository
{
    private readonly ShaloTrackDbContext _context;

    public VehicleReminderRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<VehicleReminder>> GetByVehicleAsync(Guid vehicleId)
    {
        return await _context.VehicleReminders
            .AsNoTracking()
            .Where(r => r.VehicleId == vehicleId)
            .OrderBy(r => r.DueDate)
            .ToListAsync();
    }

    public async Task<VehicleReminder?> GetByVehicleAndTypeAsync(Guid vehicleId, VehicleReminderType type)
    {
        return await _context.VehicleReminders
            .FirstOrDefaultAsync(r => r.VehicleId == vehicleId && r.Type == type);
    }

    public async Task<VehicleReminder?> GetByIdWithVehicleAsync(Guid reminderId)
    {
        return await _context.VehicleReminders
            .Include(r => r.Vehicle)
            .FirstOrDefaultAsync(r => r.ReminderId == reminderId);
    }

    public async Task AddAsync(VehicleReminder reminder)
    {
        await _context.VehicleReminders.AddAsync(reminder);
    }

    public void Remove(VehicleReminder reminder)
    {
        _context.VehicleReminders.Remove(reminder);
    }
}