namespace Maf.Agent.Api.Chat.Application;

public sealed record ChatTraceStepDraft(int Sequence, string Kind, string? RefId, string? Name, int Size);

public sealed record ChatTraceRunDraft(
    string ThreadId,
    string UserId,
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? Outcome,
    string? Model,
    long? InputTokens,
    long? OutputTokens,
    long? ReasoningTokens,
    long? TotalTokens,
    IReadOnlyList<ChatTraceStepDraft> Steps);

public interface IChatTraceStore
{
    Task SaveAsync(IReadOnlyList<ChatTraceRunDraft> runs, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatTraceThreadView>> ListThreadsAsync(
        string userId,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatTraceRunView>> ListByThreadAsync(
        string userId,
        string threadId,
        CancellationToken cancellationToken);
}
