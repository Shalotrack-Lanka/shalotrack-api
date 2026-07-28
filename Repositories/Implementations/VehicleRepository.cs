using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class VehicleRepository : IVehicleRepository
{
    private readonly ShaloTrackDbContext _context;

    public VehicleRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<Vehicle>> GetAllAsync()
    {
        // NOTE: deliberately NOT filtering by IsActive here -- this is the
        // staff-only listing, and staff likely need visibility into removed
        // vehicles too (audit/support purposes).
        return await _context.Vehicles
            .Include(v => v.Customer)
            .Include(v => v.DeviceAssignments)   // FIX: was missing — HasGpsDevice/Imei
            .ThenInclude(a => a.Device)          // were silently always false/null here
            .AsNoTracking()
            .OrderBy(v => v.VehicleNumber)
            .ToListAsync();
    }

    public async Task<Vehicle?> GetByIdAsync(Guid vehicleId)
    {
        // NOTE: deliberately NOT filtering by IsActive here -- a removed
        // vehicle's basic info may still legitimately need to be looked up
        // (e.g. viewing historical trips/alerts that reference it).
        return await _context.Vehicles
            .Include(v => v.Customer)
            .Include(v => v.DeviceAssignments)
            .ThenInclude(a => a.Device) //to get the IMEI
            .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
    }

    public async Task<List<Vehicle>> GetByCustomerAsync(Guid customerId)
    {
        // FIX: this is the customer-facing "my vehicles" list -- a removed
        // vehicle must disappear from here for its former owner, so a
        // deactivated vehicle no longer shows up once soft-deleted.
        return await _context.Vehicles
            .Include(v => v.Customer)
            .Include(v => v.DeviceAssignments)      // FIX: was missing entirely —
            .ThenInclude(a => a.Device)          // HasGpsDevice was silently always false here
            .Where(v => v.CustomerId == customerId && v.IsActive)
            .AsNoTracking()
            .OrderBy(v => v.VehicleNumber)
            .ToListAsync();
    }

    public async Task<Vehicle?> GetByVehicleNumberAsync(string vehicleNumber)
    {
        return await _context.Vehicles
            .FirstOrDefaultAsync(v => v.VehicleNumber == vehicleNumber);
    }

    public async Task<Vehicle?> GetByChassisNumberAsync(string chassisNumber)
    {
        return await _context.Vehicles
            .FirstOrDefaultAsync(v => v.ChassisNumber == chassisNumber);
    }

    public async Task<Vehicle?> GetByEngineNumberAsync(string engineNumber)
    {
        return await _context.Vehicles
            .FirstOrDefaultAsync(v => v.EngineNumber == engineNumber);
    }

    public async Task<bool> ExistsAsync(Guid vehicleId)
    {
        return await _context.Vehicles
            .AnyAsync(v => v.VehicleId == vehicleId);
    }

    public async Task AddAsync(Vehicle vehicle)
    {
        await _context.Vehicles.AddAsync(vehicle);
    }

    public void Update(Vehicle vehicle)
    {
        _context.Vehicles.Update(vehicle);
    }

    public void Delete(Vehicle vehicle)
    {
        // NOTE: this raw hard-delete method is left in place (some future,
        // genuine admin "purge" tool might legitimately need it), but
        // VehicleService.DeleteAsync() no longer calls this -- it soft-
        // deletes via IsActive instead. See VehicleService.cs.
        _context.Vehicles.Remove(vehicle);
    }
}