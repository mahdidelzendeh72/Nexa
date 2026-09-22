namespace ollama.Sessions;

public sealed class UserChatContext : IUserChatContext
{
    public UserChatContext(string defaultUserId)
    {
        UserId = defaultUserId;
    }

    public string UserId { get; private set; }

    public string? ActiveSessionId { get; private set; }

    public void SetUser(string userId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        UserId = userId.Trim();
        ActiveSessionId = null;
    }

    public void SetActiveSession(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ActiveSessionId = sessionId.Trim();
    }
}
