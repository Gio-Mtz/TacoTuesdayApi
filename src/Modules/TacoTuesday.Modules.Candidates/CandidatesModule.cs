
using Microsoft.Extensions.DependencyInjection;
using TacoTuesday.SharedKernel;

namespace TacoTuesday.Modules.Candidates;

public static class CandidatesModule
{
    public static IServiceCollection AddCandidatesModule(this IServiceCollection services)
    {
        services.AddEndpointsFrom(typeof(CandidatesModule).Assembly);
        services.AddHandlersFrom(typeof(CandidatesModule).Assembly);
        return services;
    }
}