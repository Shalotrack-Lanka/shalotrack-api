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

        return services;
    }
}