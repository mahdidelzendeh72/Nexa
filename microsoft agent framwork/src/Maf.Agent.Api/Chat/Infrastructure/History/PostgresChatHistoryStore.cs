using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Domain;
using Maf.Agent.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Chat.Infrastructure.History;

public sealed class PostgresChatHistoryStore(ApplicationDbContext dbContext) : IChatHistoryStore
{
    public async Task AppendTurnAsync(
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
        CancellationToken cancellationToken)
    {
        ChatConversation? conversation = await dbContext.ChatConversations
            .Include(item => item.Entries)
            .SingleOrDefaultAsync(item => item.ThreadId == threadId, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (conversation is null)
        {
            dbContext.ChatConversations.Add(ChatConversation.Open(
                threadId,
                ownerUserId,
                surface,
                userText,
                assistantText,
                providerId,
                modelName,
                now,
                serviceConversationId,
                attachments,
                assistantAttachments,
                usage,
                toolMessages));
        }
        else
        {
            conversation.AppendTurn(
                ownerUserId,
                surface,
                userText,
                assistantText,
                providerId,
                modelName,
                now,
                serviceConversationId,
                attachments,
                assistantAttachments,
                usage,
                toolMessages);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatConversation>> ListAsync(
        string ownerUserId,
        string surface,
        CancellationToken cancellationToken) =>
        await dbContext.ChatConversations
            .Where(item => item.OwnerUserId == ownerUserId && item.Surface == surface)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);

    public Task<ChatConversation?> FindAsync(
        string threadId,
        string ownerUserId,
        CancellationToken cancellationToken) =>
        dbContext.ChatConversations
            .Include(item => item.Entries)
            .ThenInclude(entry => entry.Attachments)
            .Include(item => item.Entries)
            .ThenInclude(entry => entry.Usage)
            .Include(item => item.Entries)
            .ThenInclude(entry => entry.ToolMessages)
            .SingleOrDefaultAsync(
                item => item.ThreadId == threadId && item.OwnerUserId == ownerUserId,
                cancellationToken);

    public async Task<bool> ReactAsync(
        string threadId,
        string ownerUserId,
        int sequence,
        ChatReaction reaction,
        CancellationToken cancellationToken)
    {
        ChatConversation? conversation = await dbContext.ChatConversations
            .Include(item => item.Entries)
            .SingleOrDefaultAsync(
                item => item.ThreadId == threadId && item.OwnerUserId == ownerUserId,
                cancellationToken);
        if (conversation is null || !conversation.React(ownerUserId, sequence, reaction, DateTimeOffset.UtcNow))
        {
            return false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
