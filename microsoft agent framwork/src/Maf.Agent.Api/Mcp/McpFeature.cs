using Maf.Agent.Api.Mcp.Application;
using Maf.Agent.Api.Mcp.Endpoints;
using Maf.Agent.Api.Mcp.Infrastructure;

namespace Maf.Agent.Api.Mcp;

public static class McpFeature
{
    public static IServiceCollection AddMcpFeature(this IServiceCollection services)
    {
        services.AddMcpApi();
        services.AddScoped<IMcpServerStore, PostgresMcpServerStore>();
        services.AddSingleton<IMcpToolPool, McpToolPool>();
        services.AddScoped<McpServerRegistry>();
        services.AddSingleton<IMcpServerSessionFactory, McpServerSessionFactory>();
        return services;
    }

    public static IEndpointRouteBuilder MapMcpFeature(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapMcpEndpoints();
}
