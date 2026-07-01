using ShaloTrack_API.Repositories.Implementations;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Services.Implementations;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Extensions;

public static class ServiceCollectionExtensions
{
    //file to register the services instead of the programs.cs
    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<ICustomerRepository, CustomerRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<ICustomerService, CustomerService>();

        return services;
    }
}