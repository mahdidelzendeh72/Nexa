using Nexa.Domain.Models;

namespace Nexa.Application.Abstractions;

public interface IModelCatalogRepository
{
    Task<ModelProvider?> GetProviderAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelProvider>> ListProvidersAsync(CancellationToken cancellationToken);
    Task<ModelProfile?> GetProfileAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelProfile>> ListProfilesAsync(CancellationToken cancellationToken);
    Task AddProfileAsync(ModelProfile profile, CancellationToken cancellationToken);
}
