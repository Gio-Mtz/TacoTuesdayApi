using Microsoft.AspNetCore.Routing;

namespace TacoTuesday.SharedKernel;
/// <summary>
/// One endpoint per class, living next to its handler in the feature folder.
/// Discovered and mapped automatically — see EndpointExtensions.
/// </summary>
public interface IEndpoint
{
    void Map(IEndpointRouteBuilder app);
}