using Maf.Agent.Api.Chat.Application;

namespace Maf.Agent.Api.Tests;

public sealed class ChatTraceTimelineTests
{
    [Fact]
    public void Stored_prefix_and_later_live_steps_stay_in_order()
    {
        ChatTraceRunView stored = Run("run_1", 1, null, Step(1, "tool", "search", 2));
        ChatTraceRunDraft live = Draft("run_1", 1, 5, StepDraft(2, "message", null, 8));

        ChatTraceRunView merged = Assert.Single(ChatTraceTimeline.Merge([stored], [live], 10));

        Assert.Equal(2, merged.Steps.Count);
        Assert.Equal(1, merged.Steps[0].Sequence);
        Assert.Equal("search", merged.Steps[0].Name);
        Assert.Equal(2, merged.Steps[1].Sequence);
        Assert.Equal("message", merged.Steps[1].Kind);
        Assert.Equal(8, merged.Steps[1].Size);
        Assert.Equal("success", merged.Outcome);
        Assert.Equal(1208, merged.TotalTokens);
    }

    [Fact]
    public void Live_size_replaces_the_stored_step_with_the_same_sequence()
    {
        ChatTraceRunView stored = Run("run_1", 1, null, Step(1, "tool", "search", 2));
        ChatTraceRunDraft live = Draft("run_1", 1, null, StepDraft(1, "tool", "search", 9));

        ChatTraceRunView merged = Assert.Single(ChatTraceTimeline.Merge([stored], [live], 10));

        ChatTraceStepView step = Assert.Single(merged.Steps);
        Assert.Equal(9, step.Size);
        Assert.Null(merged.FinishedAt);
    }

    [Fact]
    public void Open_live_run_is_included_and_the_newest_runs_are_kept()
    {
        ChatTraceRunView older = Run("run_old", 1, 2, Step(1, "message", null, 1));
        ChatTraceRunView middle = Run("run_mid", 3, 4, Step(1, "message", null, 1));
        ChatTraceRunDraft newest = Draft("run_new", 6, null, StepDraft(1, "tool", "ask", 3));

        IReadOnlyList<ChatTraceRunView> merged = ChatTraceTimeline.Merge([older, middle], [newest], 2);

        Assert.Equal(["run_new", "run_mid"], merged.Select(run => run.RunId).ToArray());
        Assert.Null(merged[0].FinishedAt);
        Assert.Equal("ask", merged[0].Steps[0].Name);
    }

    [Fact]
    public void Stored_reasoning_steps_fold_until_another_kind()
    {
        ChatTraceRunView run = Run(
            "run_1",
            1,
            2,
            Step(1, "reasoning", null, 69),
            Step(2, "reasoning", null, 96),
            Step(3, "tool", "search", 3),
            Step(4, "reasoning", null, 20));

        ChatTraceRunView folded = ChatTraceTimeline.FoldReasoning(run);

        Assert.Equal(3, folded.Steps.Count);
        Assert.Equal(165, folded.Steps[0].Size);
        Assert.Equal("tool", folded.Steps[1].Kind);
        Assert.Equal(20, folded.Steps[2].Size);
    }

    [Fact]
    public void Live_open_run_marks_its_thread_open_and_keeps_the_newer_time()
    {
        ChatTraceThreadView stored = new("thread_1", At(2), "success", "gpt-4.1", 2, false);
        ChatTraceRunDraft live = Draft("run_new", 6, null, StepDraft(1, "reasoning", null, 10));

        ChatTraceThreadView thread = Assert.Single(ChatTraceTimeline.Threads([stored], [live], 10));

        Assert.True(thread.Open);
        Assert.Equal(At(6), thread.LastRunAt);
        Assert.Equal(2, thread.RunCount);
        Assert.Equal("gpt-5", thread.Model);
    }

    private static ChatTraceRunView Run(
        string runId,
        int startedAt,
        int? finishedAt,
        params ChatTraceStepView[] steps) =>
        new(
            "thread_1",
            runId,
            At(startedAt),
            finishedAt is int end ? At(end) : null,
            finishedAt is null ? null : "success",
            "gpt-5",
            10,
            20,
            0,
            30,
            steps);

    private static ChatTraceRunDraft Draft(
        string runId,
        int startedAt,
        int? finishedAt,
        params ChatTraceStepDraft[] steps) =>
        new(
            "thread_1",
            "user",
            runId,
            At(startedAt),
            finishedAt is int end ? At(end) : null,
            finishedAt is null ? null : "success",
            "gpt-5",
            363,
            845,
            768,
            1208,
            steps);

    private static ChatTraceStepView Step(int sequence, string kind, string? name, int size) =>
        new(sequence, kind, "ref", name, size);

    private static ChatTraceStepDraft StepDraft(int sequence, string kind, string? name, int size) =>
        new(sequence, kind, "ref", name, size);

    private static DateTimeOffset At(int minute) =>
        new(2026, 11, 11, 8, minute, 0, TimeSpan.Zero);
}
