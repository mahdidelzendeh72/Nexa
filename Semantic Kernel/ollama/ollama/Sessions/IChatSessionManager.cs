namespace ollama.Sessions;

public interface IChatSessionManager
{
    string CurrentUserId { get; }

    ChatSession GetActiveSession();

    ChatSession CreateSession(string? title = null);

    void SwitchSession(string sessionId);

    IReadOnlyList<ChatSessionSummary> ListSessions();

    void SetUser(string userId);

    void EnsureActiveSession();
}
