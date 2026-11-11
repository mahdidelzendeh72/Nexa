namespace Maf.Agent.Api.Chat.Application;

public static class ChatTraceTimeline
{
    public static IReadOnlyList<ChatTraceRunView> Merge(
        IReadOnlyList<ChatTraceRunView> stored,
        IReadOnlyList<ChatTraceRunDraft> live,
        int take)
    {
        Dictionary<string, ChatTraceRunView> runs = new(StringComparer.Ordinal);
        foreach (ChatTraceRunView run in stored)
        {
            runs[run.RunId] = run;
        }

        foreach (ChatTraceRunDraft draft in live)
        {
            runs[draft.RunId] = runs.TryGetValue(draft.RunId, out ChatTraceRunView? existing)
                ? Combine(existing, draft)
                : FromDraft(draft);
        }

        return runs.Values
            .OrderByDescending(run => run.StartedAt)
            .Take(take)
            .ToArray();
    }

    public static IReadOnlyList<ChatTraceThreadView> Threads(
        IReadOnlyList<ChatTraceThreadView> stored,
        IReadOnlyList<ChatTraceRunDraft> live,
        int take)
    {
        Dictionary<string, ThreadAccumulator> threads = new(StringComparer.Ordinal);
        foreach (ChatTraceThreadView thread in stored)
        {
            threads[thread.ThreadId] = ThreadAccumulator.From(thread);
        }

        foreach (ChatTraceRunDraft draft in live)
        {
            if (!threads.TryGetValue(draft.ThreadId, out ThreadAccumulator? thread))
            {
                thread = new ThreadAccumulator(draft.ThreadId);
                threads[draft.ThreadId] = thread;
            }

            thread.Add(draft);
        }

        return threads.Values
            .Select(thread => thread.View())
            .OrderByDescending(thread => thread.LastRunAt)
            .Take(take)
            .ToArray();
    }

    public static ChatTraceRunView FoldReasoning(ChatTraceRunView run)
    {
        List<ChatTraceStepView> steps = [];
        foreach (ChatTraceStepView step in run.Steps.OrderBy(item => item.Sequence))
        {
            if (steps.Count > 0 && step.Kind == "reasoning" && steps[^1].Kind == "reasoning")
            {
                ChatTraceStepView previous = steps[^1];
                steps[^1] = previous with
                {
                    Size = previous.Size + step.Size,
                    Name = previous.Name ?? step.Name,
                };
                continue;
            }

            steps.Add(step);
        }

        return run with { Steps = steps };
    }

    private sealed class ThreadAccumulator(string threadId)
    {
        private DateTimeOffset _lastRunAt;
        private string? _outcome;
        private string? _model;
        private int _runCount;
        private bool _open;
        private readonly HashSet<string> _liveRuns = new(StringComparer.Ordinal);

        public static ThreadAccumulator From(ChatTraceThreadView thread)
        {
            ThreadAccumulator accumulator = new(thread.ThreadId)
            {
                _lastRunAt = thread.LastRunAt,
                _outcome = thread.LastOutcome,
                _model = thread.Model,
                _runCount = thread.RunCount,
                _open = thread.Open,
            };
            return accumulator;
        }

        public void Add(ChatTraceRunDraft draft)
        {
            _liveRuns.Add(draft.RunId);
            DateTimeOffset at = draft.FinishedAt ?? draft.StartedAt;
            bool newer = _lastRunAt == default || at >= _lastRunAt;
            if (newer)
            {
                _lastRunAt = at;
                if (!string.IsNullOrWhiteSpace(draft.Model))
                {
                    _model = draft.Model;
                }

                if (draft.FinishedAt is not null)
                {
                    _outcome = draft.Outcome;
                }
            }

            if (draft.FinishedAt is null)
            {
                _open = true;
            }
        }

        public ChatTraceThreadView View() =>
            new(threadId, _lastRunAt, _outcome, _model, Math.Max(_runCount, _liveRuns.Count), _open);
    }

    private static ChatTraceRunView Combine(ChatTraceRunView stored, ChatTraceRunDraft live)
    {
        Dictionary<int, ChatTraceStepView> steps = stored.Steps.ToDictionary(step => step.Sequence);
        foreach (ChatTraceStepDraft step in live.Steps)
        {
            steps[step.Sequence] = ToStep(step);
        }

        bool liveFinished = live.FinishedAt is not null;
        return new ChatTraceRunView(
            stored.ThreadId,
            stored.RunId,
            stored.StartedAt <= live.StartedAt ? stored.StartedAt : live.StartedAt,
            liveFinished ? live.FinishedAt : stored.FinishedAt,
            liveFinished ? live.Outcome : stored.Outcome,
            liveFinished ? live.Model ?? stored.Model : stored.Model ?? live.Model,
            liveFinished ? live.InputTokens ?? stored.InputTokens : stored.InputTokens ?? live.InputTokens,
            liveFinished ? live.OutputTokens ?? stored.OutputTokens : stored.OutputTokens ?? live.OutputTokens,
            liveFinished ? live.ReasoningTokens ?? stored.ReasoningTokens : stored.ReasoningTokens ?? live.ReasoningTokens,
            liveFinished ? live.TotalTokens ?? stored.TotalTokens : stored.TotalTokens ?? live.TotalTokens,
            steps.Values.OrderBy(step => step.Sequence).ToArray());
    }

    private static ChatTraceRunView FromDraft(ChatTraceRunDraft draft) =>
        new(
            draft.ThreadId,
            draft.RunId,
            draft.StartedAt,
            draft.FinishedAt,
            draft.Outcome,
            draft.Model,
            draft.InputTokens,
            draft.OutputTokens,
            draft.ReasoningTokens,
            draft.TotalTokens,
            draft.Steps.Select(ToStep).ToArray());

    private static ChatTraceStepView ToStep(ChatTraceStepDraft step) =>
        new(step.Sequence, step.Kind, step.RefId, step.Name, step.Size);
}
