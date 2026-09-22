namespace ollama.Sessions;

public interface IChatSessionStore
{
    ChatSession Create(string userId, string? title = null);

    ChatSession Get(string userId, string sessionId);

    bool TryGet(string userId, string sessionId, out ChatSession? session);

    IReadOnlyList<ChatSession> List(string userId);

    bool Remove(string userId, string sessionId);
}
