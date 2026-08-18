using Microsoft.EntityFrameworkCore;
using Nexa.Application.Abstractions;
using Nexa.Domain.Conversations;
using Nexa.Infrastructure.Persistence;

namespace Nexa.Infrastructure.Persistence.Repositories;

internal sealed class ConversationRepository(NexaDbContext db) : IConversationRepository
{
    public async Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await Query().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Conversation>> ListByUserAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken)
    {
        var query = db.Conversations.AsQueryable().Where(x => x.UserId == userId);
        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        return await query
            .AsNoTracking()
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Conversation conversation, CancellationToken cancellationToken) =>
        await db.Conversations.AddAsync(conversation, cancellationToken);

    private IQueryable<Conversation> Query() =>
        db.Conversations.Include(c => c.Messages);
}
