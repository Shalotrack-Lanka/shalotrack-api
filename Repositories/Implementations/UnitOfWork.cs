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
    public IDeviceAssignmentRepository DeviceAssignments { get; }
    public IAlertRepository Alerts { get; }                       // NEW
    public ICustomerFcmTokenRepository FcmTokens { get; }         // NEW
    public ISubscriptionRepository Subscriptions { get; }         // NEW
    public IDeviceStatusRepository DeviceStatuses { get; }        // FIX -- was declared but never assigned (CS8618); constructor now takes and wires it, same pattern as every other repository here
    public IEmergencyContactRepository EmergencyContacts { get; }  // NEW
    public ISetupShalotrackDeviceRepository SetupShalotrackDevices { get; } // NEW

    public UnitOfWork(
        ShaloTrackDbContext context,
        ICustomerRepository customerRepository,
        IVehicleRepository vehicleRepository,
        IGpsDeviceRepository gpsDeviceRepository,
        IDeviceAssignmentRepository deviceAssignmentRepository,
        IAlertRepository alertRepository,                         // NEW
        ICustomerFcmTokenRepository customerFcmTokenRepository,   // NEW
        ISubscriptionRepository subscriptionRepository,           // NEW
        IDeviceStatusRepository deviceStatusRepository,           // FIX
        IEmergencyContactRepository emergencyContactRepository,   // NEW
        ISetupShalotrackDeviceRepository setupShalotrackDeviceRepository) // NEW
    {
        _context = context;
        Customers = customerRepository;
        Vehicles = vehicleRepository;
        GpsDevices = gpsDeviceRepository;
        DeviceAssignments = deviceAssignmentRepository;
        Alerts = alertRepository;                                 // NEW
        FcmTokens = customerFcmTokenRepository;                   // NEW
        Subscriptions = subscriptionRepository;                   // NEW
        DeviceStatuses = deviceStatusRepository;                  // FIX
        EmergencyContacts = emergencyContactRepository;           // NEW
        SetupShalotrackDevices = setupShalotrackDeviceRepository; // NEW
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