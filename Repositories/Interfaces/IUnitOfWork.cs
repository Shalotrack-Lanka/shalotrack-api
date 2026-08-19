namespace ShaloTrack_API.Repositories.Interfaces;

public interface IUnitOfWork
{
    ICustomerRepository Customers { get; }
    IVehicleRepository Vehicles { get; }
    IGpsDeviceRepository GpsDevices { get; }
    IDeviceAssignmentRepository DeviceAssignments { get; }
    IAlertRepository Alerts { get; }                        // NEW
    ICustomerFcmTokenRepository FcmTokens { get; }           // NEW
    ISubscriptionRepository Subscriptions { get; }           // NEW
    IDeviceStatusRepository DeviceStatuses { get; }
    IEmergencyContactRepository EmergencyContacts { get; }   // NEW
    ISetupShalotrackDeviceRepository SetupShalotrackDevices { get; } // NEW
    ISavedPlaceRepository SavedPlaces { get; }
    IVehicleShareRepository VehicleShares { get; }
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}