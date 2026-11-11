using Maf.Agent.Api.Chat.Application;

namespace Maf.Agent.Api.Chat.Infrastructure.Ledgers;

public sealed class ChatTraceLedger
{
    public const int StepThreshold = 200;

    private readonly Dictionary<string, ConversationTrace> _threads = new(StringComparer.Ordinal);
    private readonly Lock _directory = new();

    public void Begin(string threadId, string? userId, string runId) =>
        Conversation(threadId).Begin(userId, runId);

    public void Finish(
        string threadId,
        string runId,
        string? outcome,
        string? model,
        long? inputTokens,
        long? outputTokens,
        long? reasoningTokens,
        long? totalTokens) =>
        Conversation(threadId).Finish(runId, outcome, model, inputTokens, outputTokens, reasoningTokens, totalTokens);

    public void Step(string threadId, string runId, string kind, string? refId, string? name, int size) =>
        Conversation(threadId).Step(runId, kind, refId, name, size);

    public IReadOnlyList<ChatTraceRunDraft> Snapshot(string userId)
    {
        ConversationTrace[] conversations;
        lock (_directory)
        {
            conversations = [.. _threads.Values];
        }

        List<ChatTraceRunDraft> drafts = [];
        foreach (ConversationTrace conversation in conversations)
        {
            conversation.Snapshot(drafts, userId);
        }

        return drafts;
    }

    public IReadOnlyList<ChatTraceRunDraft> Detach()
    {
        ConversationTrace[] conversations;
        lock (_directory)
        {
            conversations = [.. _threads.Values];
        }

        List<ChatTraceRunDraft> drafts = [];
        foreach (ConversationTrace conversation in conversations)
        {
            conversation.Detach(drafts, StepThreshold);
        }

        return drafts;
    }

    public void Commit(IReadOnlyList<ChatTraceRunDraft> drafts)
    {
        foreach (ChatTraceRunDraft draft in drafts)
        {
            if (Find(draft.ThreadId) is ConversationTrace conversation)
            {
                conversation.Commit(draft.RunId, draft.Steps.Count, draft.FinishedAt is not null);
            }
        }
    }

    public void Abandon(IReadOnlyList<ChatTraceRunDraft> drafts)
    {
        foreach (ChatTraceRunDraft draft in drafts)
        {
            if (Find(draft.ThreadId) is ConversationTrace conversation)
            {
                conversation.Abandon(draft.RunId);
            }
        }
    }

    private ConversationTrace Conversation(string threadId)
    {
        lock (_directory)
        {
            if (!_threads.TryGetValue(threadId, out ConversationTrace? conversation))
            {
                conversation = new ConversationTrace(threadId);
                _threads.Add(threadId, conversation);
            }

            return conversation;
        }
    }

    private ConversationTrace? Find(string threadId)
    {
        lock (_directory)
        {
            return _threads.TryGetValue(threadId, out ConversationTrace? conversation) ? conversation : null;
        }
    }

    private sealed class ConversationTrace(string threadId)
    {
        private readonly Lock _gate = new();
        private readonly List<RunTrace> _runs = [];
        private string _userId = "";

        public void Begin(string? userId, string runId)
        {
            lock (_gate)
            {
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    _userId = userId;
                }

                if (_runs.All(run => run.RunId != runId))
                {
                    _runs.Add(new RunTrace(runId));
                }
            }
        }

        public void Finish(
            string runId,
            string? outcome,
            string? model,
            long? inputTokens,
            long? outputTokens,
            long? reasoningTokens,
            long? totalTokens)
        {
            lock (_gate)
            {
                _runs.LastOrDefault(item => item.RunId == runId)
                    ?.Finish(outcome, model, inputTokens, outputTokens, reasoningTokens, totalTokens);
            }
        }

        public void Step(string runId, string kind, string? refId, string? name, int size)
        {
            lock (_gate)
            {
                RunTrace? run = _runs.LastOrDefault(item => item.RunId == runId);
                if (run is null)
                {
                    return;
                }

                if (kind == "reasoning")
                {
                    if (run.Steps.Count > run.FlushingCount && run.Steps[^1].Kind == "reasoning")
                    {
                        StepTrace tail = run.Steps[^1];
                        tail.Size += size;
                        tail.Name ??= name;
                        return;
                    }

                    run.Steps.Add(new StepTrace(run.NextSequence(), kind, refId, name, size));
                    return;
                }

                if (refId is not null)
                {
                    for (int index = run.Steps.Count - 1; index >= run.FlushingCount; index--)
                    {
                        StepTrace step = run.Steps[index];
                        if (step.Kind == kind && step.RefId == refId)
                        {
                            step.Size += size;
                            step.Name ??= name;
                            return;
                        }
                    }
                }

                run.Steps.Add(new StepTrace(run.NextSequence(), kind, refId, name, size));
            }
        }

        public void Snapshot(List<ChatTraceRunDraft> drafts, string userId)
        {
            lock (_gate)
            {
                if (!string.Equals(_userId, userId, StringComparison.Ordinal))
                {
                    return;
                }

                foreach (RunTrace run in _runs)
                {
                    drafts.Add(run.Draft(threadId, _userId, run.Steps.Count));
                }
            }
        }

        public void Detach(List<ChatTraceRunDraft> drafts, int stepThreshold)
        {
            lock (_gate)
            {
                foreach (RunTrace run in _runs)
                {
                    if (run.Flushing)
                    {
                        continue;
                    }

                    int pending = run.Steps.Count - run.FlushingCount;
                    bool closed = run.FinishedAt is not null;
                    if (!closed && pending < stepThreshold)
                    {
                        continue;
                    }

                    if (!closed && pending == 0)
                    {
                        continue;
                    }

                    run.Flushing = true;
                    run.FlushingCount = run.Steps.Count;
                    drafts.Add(run.Draft(threadId, _userId, run.FlushingCount));
                }
            }
        }

        public void Commit(string runId, int flushedSteps, bool includedFinish)
        {
            lock (_gate)
            {
                RunTrace? run = _runs.FirstOrDefault(item => item.RunId == runId);
                if (run is null)
                {
                    return;
                }

                int remove = Math.Min(flushedSteps, run.Steps.Count);
                if (remove > 0)
                {
                    run.Steps.RemoveRange(0, remove);
                }

                run.FlushingCount = 0;
                run.Flushing = false;
                if (includedFinish && run.Steps.Count == 0)
                {
                    _runs.Remove(run);
                }
            }
        }

        public void Abandon(string runId)
        {
            lock (_gate)
            {
                RunTrace? run = _runs.FirstOrDefault(item => item.RunId == runId);
                if (run is null)
                {
                    return;
                }

                run.Flushing = false;
                run.FlushingCount = 0;
            }
        }
    }

    private sealed class RunTrace(string runId)
    {
        private int _sequence;

        public string RunId { get; } = runId;

        public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

        public DateTimeOffset? FinishedAt { get; private set; }

        public string? Outcome { get; private set; }

        public string? Model { get; private set; }

        public long? InputTokens { get; private set; }

        public long? OutputTokens { get; private set; }

        public long? ReasoningTokens { get; private set; }

        public long? TotalTokens { get; private set; }

        public List<StepTrace> Steps { get; } = [];

        public bool Flushing { get; set; }

        public int FlushingCount { get; set; }

        public int NextSequence() => ++_sequence;

        public void Finish(
            string? outcome,
            string? model,
            long? inputTokens,
            long? outputTokens,
            long? reasoningTokens,
            long? totalTokens)
        {
            FinishedAt = DateTimeOffset.UtcNow;
            Outcome = outcome;
            Model = model;
            InputTokens = inputTokens;
            OutputTokens = outputTokens;
            ReasoningTokens = reasoningTokens;
            TotalTokens = totalTokens ?? (inputTokens ?? 0) + (outputTokens ?? 0);
        }

        public ChatTraceRunDraft Draft(string threadId, string userId, int count)
        {
            return new ChatTraceRunDraft(
                threadId,
                userId,
                RunId,
                StartedAt,
                FinishedAt,
                Outcome,
                Model,
                InputTokens,
                OutputTokens,
                ReasoningTokens,
                TotalTokens,
                Steps.Take(count)
                    .Select(step => new ChatTraceStepDraft(step.Sequence, step.Kind, step.RefId, step.Name, step.Size))
                    .ToArray());
        }
    }

    private sealed class StepTrace(int sequence, string kind, string? refId, string? name, int size)
    {
        public int Sequence { get; } = sequence;

        public string Kind { get; } = kind;

        public string? RefId { get; } = refId;

        public string? Name { get; set; } = name;

        public int Size { get; set; } = size;
    }
}
