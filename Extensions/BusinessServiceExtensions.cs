using ShaloTrack_API.Auth;
using ShaloTrack_API.Services.Implementations;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Extensions;

public static class BusinessServiceExtensions
{
    public static IServiceCollection AddBusinessServices(
        this IServiceCollection services)
    {
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IGpsDeviceService,GpsDeviceService>();
        services.AddScoped<IDeviceAssignmentService, DeviceAssignmentService>();
        services.AddScoped<ICurrentLocationService, CurrentLocationService>();
        services.AddScoped<IDeviceStatusService, DeviceStatusService>();
        services.AddScoped<IGpsTrackingService, GpsTrackingService>();
        services.AddScoped<IDeviceEventService, DeviceEventService>();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }
}