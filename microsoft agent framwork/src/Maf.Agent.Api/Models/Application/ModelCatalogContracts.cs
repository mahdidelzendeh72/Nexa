using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Models.Application;

public sealed record ModelProviderConnection(
    Guid ProviderId,
    string Name,
    ModelProviderKind Kind,
    Uri BaseAddress,
    string? ApiKey,
    ModelProtocol Protocol);

public sealed class ModelDiscoveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface IModelCatalog
{
    bool Supports(ModelProviderKind kind);

    Task<IReadOnlyList<DiscoveredModel>> ListAsync(
        ModelProviderConnection connection,
        CancellationToken cancellationToken);
}
