using Microsoft.Extensions.DependencyInjection;

using TacoTuesday.SharedKernel;

namespace TacoTuesday.Modules.Companies;

public static class CompaniesModule
{
    public static IServiceCollection AddCompaniesModule(this IServiceCollection services)
    {
        services.AddEndpointsFrom(typeof(CompaniesModule).Assembly);
        services.AddHandlersFrom(typeof(CompaniesModule).Assembly);

        return services;
    }
}