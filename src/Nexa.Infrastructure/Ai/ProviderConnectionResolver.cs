using Nexa.Application.Abstractions;
using Nexa.Application.Common;
using Nexa.Domain.Models;

namespace Nexa.Infrastructure.Ai;

public sealed class ModelConnectionsOptions
{
    public const string SectionName = "ModelConnections";

    public Dictionary<string, ProviderEndpointOptions> Connections { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProviderEndpointOptions
{
    public string? Endpoint { get; init; }
    public string? ApiKey { get; init; }
}

internal sealed class ProviderConnectionResolver(Microsoft.Extensions.Options.IOptions<ModelConnectionsOptions> options)
    : IProviderConnectionResolver
{
    public ProviderConnection Resolve(ModelProvider provider)
    {
        var map = options.Value.Connections;
        if (!TryGet(map, provider.Name, out var endpoint) && !TryGet(map, provider.Kind.ToString(), out endpoint))
        {
            throw new NexaException(
                ErrorCodes.ModelProviderNotConfigured,
                $"No connection is configured for provider '{provider.Name}'.",
                503);
        }

        if (string.IsNullOrWhiteSpace(endpoint.Endpoint))
        {
            throw new NexaException(
                ErrorCodes.ModelProviderNotConfigured,
                $"Provider '{provider.Name}' is missing an endpoint.",
                503);
        }

        return new ProviderConnection(endpoint.Endpoint, endpoint.ApiKey ?? string.Empty);
    }

    private static bool TryGet(
        Dictionary<string, ProviderEndpointOptions> map,
        string key,
        out ProviderEndpointOptions options)
    {
        return map.TryGetValue(key, out options!);
    }
}
