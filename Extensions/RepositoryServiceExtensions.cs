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

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}