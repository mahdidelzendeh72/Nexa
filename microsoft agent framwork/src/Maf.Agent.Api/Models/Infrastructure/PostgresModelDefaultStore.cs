using Maf.Agent.Api.Data;
using Maf.Agent.Api.Models.Application;
using Maf.Agent.Api.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Models.Infrastructure;

public sealed class PostgresModelDefaultStore(ApplicationDbContext dbContext) : IModelDefaultStore
{
    public async Task<IReadOnlyList<ModelDefault>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.ModelDefaults
            .OrderBy(item => item.Operation)
            .ToListAsync(cancellationToken);

    public void Add(ModelDefault modelDefault) => dbContext.ModelDefaults.Add(modelDefault);

    public void Remove(ModelDefault modelDefault) => dbContext.ModelDefaults.Remove(modelDefault);

    public async Task ReleaseIncompatibleAsync(
        ModelProvider provider,
        CancellationToken cancellationToken)
    {
        List<ModelDefault> assigned = await dbContext.ModelDefaults
            .Where(item => item.ProviderId == provider.Id)
            .ToListAsync(cancellationToken);
        foreach (ModelDefault item in assigned)
        {
            ProviderModel? model = provider.Models.FirstOrDefault(candidate => candidate.Name == item.ModelName);
            if (!item.Accepts(model))
            {
                dbContext.ModelDefaults.Remove(item);
            }
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
