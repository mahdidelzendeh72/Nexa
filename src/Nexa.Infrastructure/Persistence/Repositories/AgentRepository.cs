using Microsoft.EntityFrameworkCore;
using Nexa.Application.Abstractions;
using Nexa.Domain.Agents;
using Nexa.Infrastructure.Persistence;

namespace Nexa.Infrastructure.Persistence.Repositories;

internal sealed class AgentRepository(NexaDbContext db) : IAgentRepository
{
    public async Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<Agent?> GetByVersionIdAsync(Guid agentVersionId, CancellationToken cancellationToken) =>
        await Query().FirstOrDefaultAsync(a => a.Versions.Any(v => v.Id == agentVersionId), cancellationToken);

    public async Task<IReadOnlyList<Agent>> ListByOwnerAsync(Guid ownerUserId, bool includeArchived, CancellationToken cancellationToken)
    {
        var query = Query().Where(x => x.OwnerUserId == ownerUserId);
        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        return await query.OrderByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Agent>> ListByVersionIdsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken)
    {
        if (versionIds.Count == 0)
        {
            return [];
        }

        return await Query()
            .Where(a => a.Versions.Any(v => versionIds.Contains(v.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Agent agent, CancellationToken cancellationToken) =>
        await db.Agents.AddAsync(agent, cancellationToken);

    private IQueryable<Agent> Query() =>
        db.Agents.Include(a => a.Versions);
}
