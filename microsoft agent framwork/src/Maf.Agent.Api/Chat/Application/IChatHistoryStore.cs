using Maf.Agent.Api.Chat.Domain;

namespace Maf.Agent.Api.Chat.Application;

public interface IChatHistoryStore
{
    Task AppendTurnAsync(
        string threadId,
        string ownerUserId,
        string surface,
        string userText,
        string assistantText,
        Guid? providerId,
        string? modelName,
        string? serviceConversationId,
        IReadOnlyList<ChatFile>? attachments,
        IReadOnlyList<ChatFile>? assistantAttachments,
        ChatTurnUsage? usage,
        IReadOnlyList<string>? toolMessages,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatConversation>> ListAsync(
        string ownerUserId,
        string surface,
        CancellationToken cancellationToken);

    Task<ChatConversation?> FindAsync(
        string threadId,
        string ownerUserId,
        CancellationToken cancellationToken);

    Task<bool> ReactAsync(
        string threadId,
        string ownerUserId,
        int sequence,
        ChatReaction reaction,
        CancellationToken cancellationToken);
}
