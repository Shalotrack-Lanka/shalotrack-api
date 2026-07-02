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
        return await _context.Vehicles
            .Include(v => v.Customer)
            .AsNoTracking()
            .OrderBy(v => v.VehicleNumber)
            .ToListAsync();
    }

    public async Task<Vehicle?> GetByIdAsync(Guid vehicleId)
    {
        return await _context.Vehicles
            .Include(v => v.Customer)
            .Include(v => v.DeviceAssignments)
            .FirstOrDefaultAsync(v => v.VehicleId == vehicleId);
    }

    public async Task<List<Vehicle>> GetByCustomerAsync(Guid customerId)
    {
        return await _context.Vehicles
            .Include(v => v.Customer)
            .Where(v => v.CustomerId == customerId)
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
        _context.Vehicles.Remove(vehicle);
    }
}