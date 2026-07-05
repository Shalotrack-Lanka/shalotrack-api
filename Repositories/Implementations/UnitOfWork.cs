using Microsoft.EntityFrameworkCore.Storage;
using ShaloTrack_API.Data;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class UnitOfWork : IUnitOfWork
{
    private readonly ShaloTrackDbContext _context;

    private IDbContextTransaction? _transaction;

    public ICustomerRepository Customers { get; }
    public IVehicleRepository Vehicles { get; }
    public IGpsDeviceRepository GpsDevices { get; }

    public UnitOfWork(
        ShaloTrackDbContext context,
        ICustomerRepository customerRepository,
        IVehicleRepository vehicleRepository,
        IGpsDeviceRepository gpsDeviceRepository)
    {
        _context = context;
        Customers = customerRepository;
        Vehicles = vehicleRepository;
        GpsDevices = gpsDeviceRepository;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
            await _transaction.CommitAsync();
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
            await _transaction.RollbackAsync();
    }
}