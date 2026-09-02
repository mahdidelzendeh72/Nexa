using Nexa.Application.Abstractions;
using Nexa.Contracts.Conversations;
using Nexa.Contracts.Runtime;

namespace Nexa.Application.Conversations;

public interface IConversationService
{
    Task<IReadOnlyList<ConversationDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken);
    Task<ConversationDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ConversationDto> CreateAsync(CreateConversationRequest request, CancellationToken cancellationToken);
    Task<ConversationDto> RenameAsync(Guid id, RenameConversationRequest request, CancellationToken cancellationToken);
    Task ArchiveAsync(Guid id, CancellationToken cancellationToken);
    Task<SendMessageResponse> SendMessageAsync(Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken);
    IAsyncEnumerable<AgentStreamEventDto> StreamMessageAsync(Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentRunDto>> ListRunsAsync(Guid conversationId, CancellationToken cancellationToken);
}
