namespace Maf.Agent.Api.Chat.Domain;

public sealed class ChatTraceRun
{
    private readonly List<ChatTraceStep> _steps = [];

    private ChatTraceRun()
    {
        RunId = "";
        ThreadId = "";
    }

    private ChatTraceRun(string runId, string threadId, DateTimeOffset startedAt)
    {
        RunId = runId;
        ThreadId = threadId;
        StartedAt = startedAt;
    }

    public string RunId { get; private set; }

    public string ThreadId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public string? Outcome { get; private set; }

    public string? Model { get; private set; }

    public long? InputTokens { get; private set; }

    public long? OutputTokens { get; private set; }

    public long? ReasoningTokens { get; private set; }

    public long? TotalTokens { get; private set; }

    public IReadOnlyList<ChatTraceStep> Steps => _steps;

    public static ChatTraceRun Start(string runId, string threadId, DateTimeOffset startedAt) =>
        new(runId, threadId, startedAt);

    public bool Complete(
        DateTimeOffset? finishedAt,
        string? outcome,
        string? model,
        long? inputTokens,
        long? outputTokens,
        long? reasoningTokens,
        long? totalTokens)
    {
        bool newlyFinished = FinishedAt is null && finishedAt is not null;
        if (finishedAt is not null)
        {
            FinishedAt = finishedAt;
        }

        if (!string.IsNullOrWhiteSpace(outcome))
        {
            Outcome = outcome;
        }

        if (!string.IsNullOrWhiteSpace(model))
        {
            Model = model;
        }

        InputTokens ??= inputTokens;
        OutputTokens ??= outputTokens;
        ReasoningTokens ??= reasoningTokens;
        TotalTokens ??= totalTokens;
        return newlyFinished;
    }

    public void AppendStep(int sequence, string kind, string? refId, string? name, int size)
    {
        if (_steps.Any(step => step.Sequence == sequence))
        {
            return;
        }

        _steps.Add(new ChatTraceStep(RunId, sequence, kind, refId, name, size));
    }
}
