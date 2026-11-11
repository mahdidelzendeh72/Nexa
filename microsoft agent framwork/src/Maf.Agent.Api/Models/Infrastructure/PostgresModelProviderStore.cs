using Maf.Agent.Api.Data;
using Maf.Agent.Api.Models.Application;
using Maf.Agent.Api.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Models.Infrastructure;

public sealed class PostgresModelProviderStore(ApplicationDbContext dbContext) : IModelProviderStore
{
    public async Task<IReadOnlyList<ModelProvider>> ListAsync(
        CancellationToken cancellationToken) =>
        await dbContext.ModelProviders
            .Include(provider => provider.Models)
            .OrderBy(provider => provider.Name)
            .ToListAsync(cancellationToken);

    public Task<ModelProvider?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.ModelProviders
            .Include(provider => provider.Models)
            .SingleOrDefaultAsync(provider => provider.Id == id, cancellationToken);

    public async Task AddAsync(ModelProvider provider, CancellationToken cancellationToken)
    {
        dbContext.ModelProviders.Add(provider);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(ModelProvider provider, CancellationToken cancellationToken)
    {
        dbContext.ModelProviders.Remove(provider);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ThrowIfNameTakenAsync(
        Guid providerId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        bool taken = await dbContext.ModelProviders.AnyAsync(
            provider => provider.Id != providerId && provider.NormalizedName == normalizedName,
            cancellationToken);
        if (taken)
        {
            throw new ModelProviderNameConflictException(normalizedName);
        }
    }
}
