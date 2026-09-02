using Nexa.Domain.AgentRuntime;

namespace Nexa.Application.Abstractions;

public interface IAgentRunRepository
{
    Task<AgentRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentRun>> ListByConversationAsync(Guid conversationId, CancellationToken cancellationToken);
    Task AddAsync(AgentRun run, CancellationToken cancellationToken);
}
