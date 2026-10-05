using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ollama.Sessions;

public sealed class ChatSessionManager(
    IChatSessionStore store,
    IUserChatContext userContext,
    ISystemPromptBuilder systemPromptBuilder) : IChatSessionManager
{
    public string CurrentUserId => userContext.UserId;

    public ChatSession GetActiveSession()
    {
        EnsureActiveSession();

        return store.Get(userContext.UserId, userContext.ActiveSessionId!);
    }

    public ChatSession CreateSession(string? title = null)
    {
        var session = store.Create(userContext.UserId, title);
        InitializeHistory(session);
        userContext.SetActiveSession(session.SessionId);
        return session;
    }

    public void SwitchSession(string sessionId)
    {
        _ = store.Get(userContext.UserId, sessionId);
        userContext.SetActiveSession(sessionId);
    }

    public IReadOnlyList<ChatSessionSummary> ListSessions()
    {
        var activeSessionId = userContext.ActiveSessionId;

        return store.List(userContext.UserId)
            .Select(session => new ChatSessionSummary(
                session.SessionId,
                session.Title,
                session.CreatedAt,
                CountConversationMessages(session.History),
                session.SessionId == activeSessionId))
            .ToList();
    }

    public void SetUser(string userId)
    {
        userContext.SetUser(userId);
    }

    public void EnsureActiveSession()
    {
        if (!string.IsNullOrWhiteSpace(userContext.ActiveSessionId)
            && store.TryGet(userContext.UserId, userContext.ActiveSessionId, out _))
        {
            return;
        }

        var existingSessions = store.List(userContext.UserId);
        if (existingSessions.Count > 0)
        {
            userContext.SetActiveSession(existingSessions[0].SessionId);
            return;
        }

        CreateSession();
    }

    private void InitializeHistory(ChatSession session)
    {
        if (session.History.Count > 0)
        {
            return;
        }

        session.History.AddSystemMessage(systemPromptBuilder.Build());
    }

    private static int CountConversationMessages(ChatHistory history)
    {
        return history.Count(message => message.Role == AuthorRole.User || message.Role == AuthorRole.Assistant);
    }
}
