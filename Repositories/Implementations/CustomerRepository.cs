using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.DTOs.Dashboard;
using ShaloTrack_API.Data;
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
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer == null)
            return null;

        // Temporary implementation
        return new DashboardResponseDto
        {
            CustomerId = customer.CustomerId,
            CustomerName = customer.FullName,
            VehicleCount = 0,
            OnlineVehicles = 0,
            OfflineVehicles = 0,
            Vehicles = new List<DashboardVehicleDto>()
        };
    }
}