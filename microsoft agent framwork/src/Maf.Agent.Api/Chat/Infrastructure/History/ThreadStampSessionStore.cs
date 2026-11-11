#pragma warning disable MAAI001 // AgentSessionStore is experimental in Microsoft Agent Framework.

using System.Security.Claims;
using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Infrastructure.Clients;
using Maf.Agent.Api.Chat.Domain;
using Maf.Agent.Api.Models.Domain;
using Microsoft.Agents.AI;

namespace Maf.Agent.Api.Chat.Infrastructure.History;

public sealed class ThreadStampSessionStore(
    IHttpContextAccessor http,
    IServiceScopeFactory scopes,
    ChatModelCatalog catalog) : AgentSessionStore
{
    public const string ThreadKey = "maf.chat.thread";

    public override ValueTask SaveSessionAsync(
        AIAgent agent,
        AgentSessionStoreKey key,
        AgentSession session,
        CancellationToken cancellationToken = default) =>
        default;

    public override ValueTask DeleteSessionAsync(
        AIAgent agent,
        AgentSessionStoreKey key,
        CancellationToken cancellationToken = default) =>
        default;

    public override ValueTask<AgentSession?> GetSessionAsync(
        AIAgent agent,
        AgentSessionStoreKey key,
        CancellationToken cancellationToken = default) =>
        new((AgentSession?)null);

    public override async ValueTask<AgentSession> GetOrCreateSessionAsync(
        AIAgent agent,
        AgentSessionStoreKey key,
        CancellationToken cancellationToken = default)
    {
        string? serviceConversationId = ChatConversation.IsThreadId(key.SessionId)
            ? await ServiceConversationIdAsync(key.SessionId, cancellationToken)
            : null;
        AgentSession session = serviceConversationId is null
            ? await agent.CreateSessionAsync(cancellationToken)
            : await CreateServiceSessionAsync(agent, serviceConversationId, cancellationToken);
        if (ChatConversation.IsThreadId(key.SessionId))
        {
            session.StateBag.SetValue(ThreadKey, key.SessionId);
        }

        return session;
    }

    private static async ValueTask<AgentSession> CreateServiceSessionAsync(
        AIAgent agent,
        string serviceConversationId,
        CancellationToken cancellationToken)
    {
        ChatClientAgent? chat = agent.GetService<ChatClientAgent>();
        return chat is null
            ? await agent.CreateSessionAsync(cancellationToken)
            : await chat.CreateSessionAsync(serviceConversationId, cancellationToken);
    }

    private async Task<string?> ServiceConversationIdAsync(string threadId, CancellationToken cancellationToken)
    {
        HttpContext? httpContext = http.HttpContext;
        if (httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) is not string ownerUserId ||
            !Guid.TryParse(httpContext.Request.Headers[SelectedChatClient.ProviderHeader], out Guid providerId) ||
            string.IsNullOrWhiteSpace(httpContext.Request.Headers[SelectedChatClient.ModelHeader]))
        {
            return null;
        }

        string modelName = Uri.UnescapeDataString(httpContext.Request.Headers[SelectedChatClient.ModelHeader].ToString());
        ChatConnection connection = await catalog.RequireAsync(providerId, modelName, cancellationToken);
        if (connection.Protocol != ModelProtocol.Responses)
        {
            return null;
        }

        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IChatHistoryStore store = scope.ServiceProvider.GetRequiredService<IChatHistoryStore>();
        ChatConversation? conversation = await store.FindAsync(threadId, ownerUserId, cancellationToken);
        return string.IsNullOrWhiteSpace(conversation?.ServiceConversationId)
            ? null
            : conversation.ServiceConversationId;
    }
}
