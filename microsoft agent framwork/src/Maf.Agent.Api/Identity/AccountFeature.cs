using Maf.Agent.Api.Identity.Application;
using Maf.Agent.Api.Identity.Endpoints;

namespace Maf.Agent.Api.Identity;

public static class AccountFeature
{
    public static IServiceCollection AddAccountFeature(this IServiceCollection services)
    {
        services.AddAccountApi();
        services.AddScoped<AccountRegistry>();
        return services;
    }

    public static IEndpointRouteBuilder MapAccountFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapAccountEndpoints();
}
