using Microsoft.AspNetCore.Routing;

namespace TacoTuesday.SharedKernel;

public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}