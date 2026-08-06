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
        services.AddScoped<IGpsDeviceService, GpsDeviceService>();
        services.AddScoped<IDeviceAssignmentService, DeviceAssignmentService>();
        services.AddScoped<ICurrentLocationService, CurrentLocationService>();
        services.AddScoped<IDeviceStatusService, DeviceStatusService>();
        services.AddScoped<IGpsTrackingService, GpsTrackingService>();
        services.AddScoped<IDeviceEventService, DeviceEventService>();
        services.AddScoped<IAlertService, AlertService>();   // NEW
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IRoadSnappingService, RoadSnappingService>();   // NEW -- live trail road-snapping
        services.AddScoped<ISubscriptionService, SubscriptionService>();   // NEW -- subscription lifecycle
        // Only implementation of IPaymentProvider today -- no payment
        // gateway merchant account yet. Swap/extend this line when a real
        // gateway (PayHere, etc.) is integrated; nothing else needs to
        // change.
        services.AddScoped<IPaymentProvider, ManualPaymentProvider>();     // NEW
        services.AddScoped<IEmergencyContactService, EmergencyContactService>();  // NEW
        return services;
    }
}