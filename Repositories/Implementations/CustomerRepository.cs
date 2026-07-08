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

    public async Task<DashboardResponseDto?> GetDashboardAsync(
    Guid customerId)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.CustomerId == customerId);

        if (customer == null)
            return null;

        var vehicles = await
            (from vehicle in _context.Vehicles

             where vehicle.CustomerId == customerId

             join assignment in _context.DeviceAssignments
                 on vehicle.VehicleId equals assignment.VehicleId
                 into assignments

             from assignment in assignments.DefaultIfEmpty()

             join location in _context.CurrentLocations
                 on vehicle.VehicleId equals location.VehicleId
                 into locations

             from location in locations.DefaultIfEmpty()

             join status in _context.DeviceStatuses
                 on assignment.DeviceId equals status.DeviceId
                 into statuses

             from status in statuses.DefaultIfEmpty()

             select new DashboardVehicleDto
             {
                 VehicleId = vehicle.VehicleId,

                 VehicleNumber = vehicle.VehicleNumber,

                 Make = vehicle.Make,

                 Model = vehicle.Model,

                 DeviceId = assignment != null
                     ? assignment.DeviceId
                     : null,

                 Latitude = location != null
                     ? location.Latitude
                     : null,

                 Longitude = location != null
                     ? location.Longitude
                     : null,

                 Speed = location != null
                     ? location.Speed
                     : 0,

                 Heading = location != null
                     ? location.Heading
                     : 0,

                 Ignition = location != null &&
                             location.IgnitionStatus,

                 LastUpdate = location != null
                     ? location.LastUpdate
                     : null,

                 Online = status != null &&
                          status.IsOnline
             })
            .OrderBy(v => v.VehicleNumber)
            .ToListAsync();

        return new DashboardResponseDto
        {
            CustomerId = customer.CustomerId,

            CustomerName = customer.FullName,

            VehicleCount = vehicles.Count,

            OnlineVehicles = vehicles.Count(v => v.Online),

            OfflineVehicles = vehicles.Count(v => !v.Online),

            Vehicles = vehicles
        };
    }
}