namespace Maf.Agent.Api.Chat.Application;

public sealed record ChatTraceThreadView(
    string ThreadId,
    DateTimeOffset LastRunAt,
    string? LastOutcome,
    string? Model,
    int RunCount,
    bool Open);

public sealed record ChatTraceStepView(int Sequence, string Kind, string? RefId, string? Name, int Size);

public sealed record ChatTraceRunView(
    string ThreadId,
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? Outcome,
    string? Model,
    long? InputTokens,
    long? OutputTokens,
    long? ReasoningTokens,
    long? TotalTokens,
    IReadOnlyList<ChatTraceStepView> Steps);
