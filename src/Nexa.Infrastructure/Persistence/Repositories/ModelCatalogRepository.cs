using Microsoft.EntityFrameworkCore;
using Nexa.Application.Abstractions;
using Nexa.Domain.Models;
using Nexa.Infrastructure.Persistence;

namespace Nexa.Infrastructure.Persistence.Repositories;

internal sealed class ModelCatalogRepository(NexaDbContext db) : IModelCatalogRepository
{
    public Task<ModelProvider?> GetProviderAsync(Guid id, CancellationToken cancellationToken) =>
        db.ModelProviders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ModelProvider>> ListProvidersAsync(CancellationToken cancellationToken) =>
        await db.ModelProviders.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<ModelProfile?> GetProfileAsync(Guid id, CancellationToken cancellationToken) =>
        db.ModelProfiles.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ModelProfile>> ListProfilesAsync(CancellationToken cancellationToken) =>
        await db.ModelProfiles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public async Task AddProfileAsync(ModelProfile profile, CancellationToken cancellationToken) =>
        await db.ModelProfiles.AddAsync(profile, cancellationToken);
}
