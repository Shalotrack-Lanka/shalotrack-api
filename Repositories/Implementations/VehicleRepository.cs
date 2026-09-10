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
        // NOTE: deliberately NOT filtering by IsActive here — this is the
        // staff-only listing, and staff likely need visibility into removed
        // vehicles too (audit/support purposes).
        return await _context.Vehicles
            .Include(v => v.Customer)
            .Include(v => v.DeviceAssignments)
            .ThenInclude(a => a.Device)
            .AsNoTracking()
            .OrderBy(v => v.VehicleNumber)
            .ToListAsync();
    }

    public async Task<Vehicle?> GetByIdAsync(Guid vehicleId)
    {
        // NOTE: deliberately NOT filtering by IsActive here — a removed
        // vehicle's basic info may still legitimately need to be looked up
        // (e.g. viewing historical trips/alerts that reference it).
        return await _context.Vehicles
            .Include(v => v.Customer)
            .Include(v => v.DeviceAssignments)
            .ThenInclude(a => a.Device)
            .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
    }

    /// <summary>
    /// PERFORMANCE FIX: Lightweight ownership check — loads Customer only.
    ///
    /// The original GetByIdAsync() loads the full DeviceAssignment history
    /// (all past and active assignments + their Device records) on every call.
    /// Every ownership check in every service fired GetByIdAsync(), meaning
    /// every single API request was loading data it didn't need just to
    /// compare vehicle.Customer.FirebaseUid against the caller's UID.
    ///
    /// This method exists for that single purpose: verify ownership.
    /// Any code path that only needs Customer.FirebaseUid must use this
    /// instead of GetByIdAsync().
    ///
    /// Callers that need IMEI or full assignment data (VehicleService.GetByIdAsync,
    /// DTO mapping, IMEI display) continue to use GetByIdAsync().
    /// </summary>
    public async Task<Vehicle?> GetByIdForOwnershipCheckAsync(Guid vehicleId)
    {
        return await _context.Vehicles
            .Include(v => v.Customer)
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
    }

    public async Task<List<Vehicle>> GetByCustomerAsync(Guid customerId)
    {
        // FIX: this is the customer-facing "my vehicles" list — a deactivated
        // vehicle must disappear from here for its former owner.
        return await _context.Vehicles
            .Include(v => v.Customer)
            .Include(v => v.DeviceAssignments)
            .ThenInclude(a => a.Device)
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

    public async Task<Vehicle?> GetDemoVehicleAsync()
    {
        return await _context.Vehicles
            .Include(v => v.CurrentLocation)
            .FirstOrDefaultAsync(v => v.IsDemoVehicle);
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
        // VehicleService.DeleteAsync() no longer calls this — it soft-deletes
        // via IsActive instead. See VehicleService.cs.
        _context.Vehicles.Remove(vehicle);
    }
}