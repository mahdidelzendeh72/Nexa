using Nexa.Domain.Agents;

namespace Nexa.Application.Abstractions;

public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Agent?> GetByVersionIdAsync(Guid agentVersionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Agent>> ListByOwnerAsync(Guid ownerUserId, bool includeArchived, CancellationToken cancellationToken);
    Task<IReadOnlyList<Agent>> ListByVersionIdsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken);
    Task AddAsync(Agent agent, CancellationToken cancellationToken);
}
