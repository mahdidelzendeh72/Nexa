using System.Security.Claims;
using System.Text;
using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Domain;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Chat.Infrastructure.History;

public sealed class StoredChatHistoryProvider(
    IHttpContextAccessor http,
    IServiceScopeFactory scopes) : ChatHistoryProvider
{
    protected override async ValueTask<IEnumerable<ChatMessage>> ProvideChatHistoryAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        if (await ConversationAsync(context.Agent, context.Session, cancellationToken) is not ChatConversation conversation)
        {
            return [];
        }

        List<ChatMessage> messages = [];
        foreach (ChatEntry entry in conversation.Entries.OrderBy(item => item.Sequence))
        {
            if (entry.Role == ChatSpeaker.Assistant)
            {
                foreach (ChatToolMessage tool in entry.ToolMessages.OrderBy(item => item.Sequence))
                {
                    if (ChatToolMessages.Read(tool.Payload) is ChatMessage toolMessage)
                    {
                        messages.Add(toolMessage);
                    }
                }
            }

            messages.Add(ToMessage(entry));
        }

        return messages;
    }

    private static ChatMessage ToMessage(ChatEntry entry)
    {
        ChatRole role = ToRole(entry.Role);
        if (entry.Attachments.Count == 0)
        {
            return new ChatMessage(role, entry.Text);
        }

        List<AIContent> contents = [];
        if (!string.IsNullOrEmpty(entry.Text))
        {
            contents.Add(new TextContent(entry.Text));
        }

        foreach (ChatAttachment attachment in entry.Attachments.OrderBy(item => item.Sequence))
        {
            if (Link(attachment) is Uri link)
            {
                contents.Add(new UriContent(link, AssistantReplyFiles.LinkMediaType));
                continue;
            }

            contents.Add(new DataContent(Convert.FromBase64String(attachment.ContentBase64), attachment.MediaType)
            {
                Name = attachment.FileName,
            });
        }

        return new ChatMessage(role, contents);
    }

    private static Uri? Link(ChatAttachment attachment)
    {
        if (attachment.MediaType != AssistantReplyFiles.LinkMediaType)
        {
            return null;
        }

        string text = Encoding.UTF8.GetString(Convert.FromBase64String(attachment.ContentBase64));
        return Uri.TryCreate(text, UriKind.Absolute, out Uri? uri) && uri.Scheme is "http" or "https"
            ? uri
            : null;
    }

    protected override ValueTask InvokedCoreAsync(InvokedContext context, CancellationToken cancellationToken = default) =>
        default;

    private async Task<ChatConversation?> ConversationAsync(
        AIAgent agent,
        AgentSession? session,
        CancellationToken cancellationToken)
    {
        if (session?.StateBag.TryGetValue(ThreadStampSessionStore.ThreadKey, out string? threadId) != true ||
            threadId is null ||
            http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) is not string ownerUserId ||
            agent.Name is not string surface ||
            !ChatConversation.IsSurface(surface))
        {
            return null;
        }

        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IChatHistoryStore store = scope.ServiceProvider.GetRequiredService<IChatHistoryStore>();
        ChatConversation? conversation = await store.FindAsync(threadId, ownerUserId, cancellationToken);
        return conversation is not null && string.Equals(conversation.Surface, surface, StringComparison.Ordinal)
            ? conversation
            : null;
    }

    private static ChatRole ToRole(ChatSpeaker role) =>
        role == ChatSpeaker.User ? ChatRole.User : ChatRole.Assistant;
}
