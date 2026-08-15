using ShaloTrack_API.Repositories.Implementations;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Extensions;

public static class RepositoryServiceExtensions
{
    public static IServiceCollection AddRepositoryServices(
        this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IGpsDeviceRepository, GpsDeviceRepository>();
        services.AddScoped<IDeviceAssignmentRepository, DeviceAssignmentRepository>();
        services.AddScoped<ICurrentLocationRepository, CurrentLocationRepository>();
        services.AddScoped<IDeviceStatusRepository, DeviceStatusRepository>();
        services.AddScoped<IGpsTrackingRepository, GpsTrackingRepository>();
        services.AddScoped<IDeviceEventRepository, DeviceEventRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();                        // NEW
        services.AddScoped<ICustomerFcmTokenRepository, CustomerFcmTokenRepository>();  // NEW
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();          // NEW
        services.AddScoped<IRawPacketRepository, RawPacketRepository>();                // NEW -- Phase 3b
        services.AddScoped<IEmergencyContactRepository, EmergencyContactRepository>();  // NEW
        services.AddScoped<ISetupShalotrackDeviceRepository, SetupShalotrackDeviceRepository>(); // NEW
        services.AddScoped<ISavedPlaceRepository, SavedPlaceRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}