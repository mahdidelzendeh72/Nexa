namespace ollama.Sessions;

public interface IUserChatContext
{
    string UserId { get; }

    string? ActiveSessionId { get; }

    void SetUser(string userId);

    void SetActiveSession(string sessionId);
}
