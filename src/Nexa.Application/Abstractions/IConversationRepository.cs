using Nexa.Domain.Conversations;

namespace Nexa.Application.Abstractions;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Conversation>> ListByUserAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken);
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken);
}
