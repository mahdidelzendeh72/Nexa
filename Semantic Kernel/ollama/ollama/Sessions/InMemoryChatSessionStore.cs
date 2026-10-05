using System.Collections.Concurrent;

namespace ollama.Sessions;

public sealed class InMemoryChatSessionStore : IChatSessionStore
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ChatSession>> _sessions = new();

    public ChatSession Create(string userId, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var session = new ChatSession
        {
            SessionId = Guid.NewGuid().ToString("N")[..8],
            UserId = userId,
            Title = title
        };

        var userSessions = _sessions.GetOrAdd(userId, _ => new ConcurrentDictionary<string, ChatSession>());
        userSessions[session.SessionId] = session;

        return session;
    }

    public ChatSession Get(string userId, string sessionId)
    {
        if (!TryGet(userId, sessionId, out var session) || session is null)
        {
            throw new KeyNotFoundException($"Session '{sessionId}' was not found for user '{userId}'.");
        }

        return session;
    }

    public bool TryGet(string userId, string sessionId, out ChatSession? session)
    {
        session = null;

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        return _sessions.TryGetValue(userId, out var userSessions)
            && userSessions.TryGetValue(sessionId, out session);
    }

    public IReadOnlyList<ChatSession> List(string userId)
    {
        if (!_sessions.TryGetValue(userId, out var userSessions))
        {
            return [];
        }

        return userSessions.Values
            .OrderByDescending(session => session.CreatedAt)
            .ToList();
    }

    public bool Remove(string userId, string sessionId)
    {
        if (!_sessions.TryGetValue(userId, out var userSessions))
        {
            return false;
        }

        return userSessions.TryRemove(sessionId, out _);
    }
}
