using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Models.Application;

public sealed record SaveModelDefault(
    ModelOperation Operation,
    Guid? ProviderId,
    string? ModelName);

public sealed record ModelDefaultChoice(Guid ProviderId, string ProviderName, string ModelName);

public sealed record ModelDefaultSelection(
    ModelOperation Operation,
    ModelDefaultChoice? Selected,
    IReadOnlyList<ModelDefaultChoice> Choices);

public interface IModelDefaultStore
{
    Task<IReadOnlyList<ModelDefault>> ListAsync(CancellationToken cancellationToken);

    void Add(ModelDefault modelDefault);

    void Remove(ModelDefault modelDefault);

    Task ReleaseIncompatibleAsync(ModelProvider provider, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
