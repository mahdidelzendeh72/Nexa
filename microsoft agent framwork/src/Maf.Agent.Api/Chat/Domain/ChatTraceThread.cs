namespace Maf.Agent.Api.Chat.Domain;

public sealed class ChatTraceThread
{
    private ChatTraceThread()
    {
        ThreadId = "";
        UserId = "";
    }

    private ChatTraceThread(string threadId, string userId, DateTimeOffset createdAt)
    {
        ThreadId = threadId;
        UserId = userId;
        LastRunAt = createdAt;
    }

    public string ThreadId { get; private set; }

    public string UserId { get; private set; }

    public int RunCount { get; private set; }

    public DateTimeOffset LastRunAt { get; private set; }

    public string? LastOutcome { get; private set; }

    public long TotalTokens { get; private set; }

    public static ChatTraceThread Open(string threadId, string userId, DateTimeOffset createdAt) =>
        new(threadId, userId, createdAt);

    public void RememberUser(string userId)
    {
        if (UserId.Length == 0 && userId.Length > 0)
        {
            UserId = userId;
        }
    }

    public void NoteRun(bool isNew, DateTimeOffset? finishedAt, string? outcome, long addedTokens)
    {
        if (isNew)
        {
            RunCount++;
        }

        if (finishedAt is DateTimeOffset at && at >= LastRunAt)
        {
            LastRunAt = at;
        }

        if (!string.IsNullOrWhiteSpace(outcome))
        {
            LastOutcome = outcome;
        }

        if (addedTokens > 0)
        {
            TotalTokens += addedTokens;
        }
    }
}
