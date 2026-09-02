using Nexa.Domain.Models;

namespace Nexa.Application.Abstractions;

public sealed record ProviderConnection(string Endpoint, string ApiKey);

public interface IProviderConnectionResolver
{
    ProviderConnection Resolve(ModelProvider provider);
}
