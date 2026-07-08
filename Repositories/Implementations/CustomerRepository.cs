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
        // SRE Optimization: Combine both table lookups into a single SQL statement execution
        return await _context.Customers
            .AsNoTracking()
            .Where(c => c.CustomerId == customerId)
            .Select(c => new DashboardResponseDto
            {
                CustomerId = c.CustomerId,
                CustomerName = c.FullName,
                VehicleCount = c.Vehicles.Count,
                OnlineVehicles = 0,
                OfflineVehicles = c.Vehicles.Count,
                Vehicles = c.Vehicles
                    .OrderBy(v => v.VehicleNumber)
                    .Select(v => new DashboardVehicleDto
                    {
                        VehicleId = v.VehicleId,
                        VehicleNumber = v.VehicleNumber,
                        Make = v.Make,
                        Model = v.Model,
                        DeviceId = null,
                        Latitude = null,
                        Longitude = null,
                        Speed = 0,
                        Heading = 0,
                        Ignition = false,
                        Online = false,
                        LastUpdate = null
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();
    }
}