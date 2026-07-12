using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.Dashboard;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class CustomerRepository : ICustomerRepository
{
    private readonly ShaloTrackDbContext _context;

    public CustomerRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<Customer?> GetByFirebaseUidAsync(string firebaseUid)
    {
        return await _context.Customers
            .FirstOrDefaultAsync(c => c.FirebaseUid == firebaseUid);
    }

    public async Task<List<Customer>> GetAllAsync()
    {
        return await _context.Customers
            .AsNoTracking()
            .OrderBy(c => c.FullName)
            .ToListAsync();
    }

    public async Task<Customer?> GetByIdAsync(Guid customerId)
    {
        return await _context.Customers
            .Include(c => c.Vehicles)
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);
    }

    public async Task<Customer?> GetByEmailAsync(string email)
    {
        return await _context.Customers
            .FirstOrDefaultAsync(c => c.Email == email);
    }

    public async Task<Customer?> GetByNicAsync(string nicNumber)
    {
        return await _context.Customers
            .FirstOrDefaultAsync(c => c.NicNumber == nicNumber);
    }

    public async Task<bool> ExistsAsync(Guid customerId)
    {
        return await _context.Customers
            .AnyAsync(c => c.CustomerId == customerId);
    }

    public async Task AddAsync(Customer customer)
    {
        await _context.Customers.AddAsync(customer);
    }

    public void Update(Customer customer)
    {
        _context.Customers.Update(customer);
    }

    public void Delete(Customer customer)
    {
        _context.Customers.Remove(customer);
    }

    public async Task<DashboardResponseDto?> GetDashboardAsync(Guid customerId)
    {
        // "Online" = device has reported within the last 10 minutes. The V5 device
        // reports every 300s (5 min) while stopped, so this gives one full missed
        // cycle of buffer before a parked-but-healthy vehicle looks falsely offline.
        var onlineThreshold = DateTime.UtcNow.AddMinutes(-10);

        var dashboard = await _context.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => new DashboardResponseDto
            {
                CustomerId = c.CustomerId,
                CustomerName = c.FullName,
                VehicleCount = c.Vehicles.Count,
                Vehicles = c.Vehicles
                    .OrderBy(v => v.VehicleNumber)
                    .Select(v => new DashboardVehicleDto
                    {
                        VehicleId = v.VehicleId,
                        VehicleNumber = v.VehicleNumber,
                        Make = v.Make,
                        Model = v.Model,
                        // FIX: these five were hardcoded to null/0/false before.
                        // Now pulled from the real CurrentLocation nav property.
                        DeviceId = v.CurrentLocation != null ? v.CurrentLocation.DeviceId : (Guid?)null,
                        Latitude = v.CurrentLocation != null ? v.CurrentLocation.Latitude : (decimal?)null,
                        Longitude = v.CurrentLocation != null ? v.CurrentLocation.Longitude : (decimal?)null,
                        Speed = v.CurrentLocation != null ? v.CurrentLocation.Speed : 0,
                        Heading = v.CurrentLocation != null ? v.CurrentLocation.Heading : 0,
                        Online = v.CurrentLocation != null && v.CurrentLocation.LastUpdate >= onlineThreshold,
                        Ignition = v.CurrentLocation != null && v.CurrentLocation.IgnitionStatus,
                        LastUpdate = v.CurrentLocation != null ? v.CurrentLocation.LastUpdate : (DateTime?)null
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (dashboard is null) return null;

        // Computed after materializing — simpler and safer than trying to get EF
        // to translate conditional aggregate counts inside the same query.
        dashboard.OnlineVehicles = dashboard.Vehicles.Count(v => v.Online);
        dashboard.OfflineVehicles = dashboard.VehicleCount - dashboard.OnlineVehicles;

        return dashboard;
    }
}