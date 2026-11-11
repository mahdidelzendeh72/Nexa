using Maf.Agent.Api.Models.Application;
using Maf.Agent.Api.Models.Endpoints;
using Maf.Agent.Api.Models.Infrastructure;

namespace Maf.Agent.Api.Models;

public static class ModelFeature
{
    public static IServiceCollection AddModelFeature(this IServiceCollection services)
    {
        services.AddModelApi();
        services.AddScoped<IModelProviderStore, PostgresModelProviderStore>();
        services.AddScoped<IModelDefaultStore, PostgresModelDefaultStore>();
        services.AddScoped<ModelProviderRegistry>();
        services.AddScoped<ModelDefaultRegistry>();
        services.AddSingleton<IModelCatalog, OpenAiModelCatalog>();
        services.AddSingleton<IModelCatalog, OllamaModelCatalog>();
        services.AddHttpClient(ModelProviderHttp.ClientName, client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        });
        return services;
    }

    public static IEndpointRouteBuilder MapModelFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapModelEndpoints();
}
