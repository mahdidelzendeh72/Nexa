using Microsoft.EntityFrameworkCore;
using Nexa.Application.Abstractions;
using Nexa.Domain.AgentRuntime;
using Nexa.Infrastructure.Persistence;

namespace Nexa.Infrastructure.Persistence.Repositories;

internal sealed class AgentRunRepository(NexaDbContext db) : IAgentRunRepository
{
    public async Task<AgentRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AgentRun>> ListByConversationAsync(Guid conversationId, CancellationToken cancellationToken) =>
        await Query()
            .Where(x => x.ConversationId == conversationId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(AgentRun run, CancellationToken cancellationToken) =>
        await db.AgentRuns.AddAsync(run, cancellationToken);

    private IQueryable<AgentRun> Query() =>
        db.AgentRuns.Include(x => x.ToolExecutions);
}
