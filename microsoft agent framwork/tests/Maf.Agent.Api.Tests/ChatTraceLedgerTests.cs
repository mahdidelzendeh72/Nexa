using System.Text;
using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Infrastructure.Ledgers;
using Maf.Agent.Api.Chat.Infrastructure.Middleware;

namespace Maf.Agent.Api.Tests;

public sealed class ChatTraceLedgerTests
{
    [Fact]
    public void One_run_keeps_a_single_tool_step_and_its_size()
    {
        ChatTraceLedger ledger = new();
        ledger.Begin("thread_1", "user", "run_1");
        ledger.Step("thread_1", "run_1", "tool", "call_1", "capture_page_screenshot", 0);
        ledger.Step("thread_1", "run_1", "tool", "call_1", null, 2);
        ledger.Finish("thread_1", "run_1", "success", "gpt-5-2025-08-07", 363, 845, 768, 1208);

        ChatTraceRunDraft draft = Assert.Single(ledger.Detach());
        ChatTraceStepDraft step = Assert.Single(draft.Steps);
        Assert.Equal("capture_page_screenshot", step.Name);
        Assert.Equal(2, step.Size);
        Assert.Equal(1208, draft.TotalTokens);
        Assert.Empty(ledger.Detach());

        ledger.Commit([draft]);
        Assert.Empty(ledger.Detach());
    }

    [Fact]
    public void Snapshot_keeps_one_users_open_run_available_for_flush()
    {
        ChatTraceLedger ledger = new();
        ledger.Begin("thread_1", "user-a", "run_1");
        ledger.Step("thread_1", "run_1", "tool", "call_1", "search", 3);
        ledger.Begin("thread_2", "user-b", "run_2");
        ledger.Step("thread_2", "run_2", "message", "msg_1", null, 4);
        ledger.Finish("thread_1", "run_1", "success", "gpt-5", 1, 2, 0, 3);
        ledger.Finish("thread_2", "run_2", "success", "gpt-5", 1, 2, 0, 3);

        Assert.Empty(ledger.Snapshot("user-c"));
        ChatTraceRunDraft own = Assert.Single(ledger.Snapshot("user-a"));
        Assert.Equal("run_1", own.RunId);
        Assert.Equal("search", Assert.Single(own.Steps).Name);

        ChatTraceRunDraft[] detached = ledger.Detach().ToArray();
        Assert.Contains(detached, draft => draft.RunId == "run_1");
        Assert.Contains(detached, draft => draft.RunId == "run_2");
    }

    [Fact]
    public void Consecutive_reasoning_steps_add_their_sizes_until_another_kind()
    {
        ChatTraceLedger ledger = new();
        ledger.Begin("thread_1", "user", "run_1");
        ledger.Step("thread_1", "run_1", "reasoning", "a", null, 10);
        ledger.Step("thread_1", "run_1", "reasoning", "b", null, 15);
        ledger.Step("thread_1", "run_1", "tool", "call_1", "search", 3);
        ledger.Step("thread_1", "run_1", "reasoning", "c", null, 4);
        ledger.Finish("thread_1", "run_1", "success", "gpt-5", 1, 2, 0, 3);

        ChatTraceRunDraft draft = Assert.Single(ledger.Detach());
        Assert.Equal(3, draft.Steps.Count);
        Assert.Equal(("reasoning", 25), (draft.Steps[0].Kind, draft.Steps[0].Size));
        Assert.Equal(("tool", 3), (draft.Steps[1].Kind, draft.Steps[1].Size));
        Assert.Equal(("reasoning", 4), (draft.Steps[2].Kind, draft.Steps[2].Size));
    }

    [Fact]
    public void Text_deltas_of_one_message_add_their_sizes()
    {
        ChatTraceLedger ledger = new();
        ledger.Begin("thread_1", "user", "run_2");
        ledger.Step("thread_1", "run_2", "message", "msg_1", null, 2);
        ledger.Step("thread_1", "run_2", "message", "msg_1", null, 2);
        ledger.Finish("thread_1", "run_2", "success", "gpt-5-2025-08-07", 361, 48, 0, 409);

        ChatTraceRunDraft draft = Assert.Single(ledger.Detach());
        ChatTraceStepDraft step = Assert.Single(draft.Steps);
        Assert.Equal("message", step.Kind);
        Assert.Equal(4, step.Size);
    }

    [Fact]
    public void Run_events_are_read_without_the_rest_of_the_payload()
    {
        byte[] finished = Encoding.UTF8.GetBytes(
            "data: {\"type\":\"RUN_FINISHED\",\"threadId\":\"thread_7qTfRzBc9nFp3wf39rdEuZnN\",\"runId\":\"run_8iXoI4TTrriRHOAyRORnlZ9c\",\"outcome\":{\"type\":\"success\"},\"usage\":[{\"model\":\"gpt-5-2025-08-07\",\"inputTokens\":363,\"outputTokens\":845,\"totalTokens\":1208,\"reasoningTokens\":768,\"cachedInputTokens\":0}]}\n\n");

        Assert.True(AgUiTraceSignals.TryRead(finished, out AgUiTraceSignal signal));
        Assert.Equal("RUN_FINISHED", signal.Type);
        Assert.Equal("thread_7qTfRzBc9nFp3wf39rdEuZnN", signal.ThreadId);
        Assert.Equal("run_8iXoI4TTrriRHOAyRORnlZ9c", signal.RunId);
        Assert.Equal("success", signal.Outcome);
        Assert.Equal(363, signal.InputTokens);
        Assert.Equal(1208, signal.TotalTokens);
    }

    [Fact]
    public void Reasoning_size_is_the_event_length_and_its_body_is_not_copied()
    {
        string json = "{\"type\":\"REASONING_ENCRYPTED_VALUE\",\"entityId\":\"msg_1\",\"encryptedValue\":\"" + new string('a', 5000) + "\"}";
        byte[] frame = Encoding.UTF8.GetBytes("data: " + json + "\n\n");

        Assert.True(AgUiTraceSignals.TryRead(frame, out AgUiTraceSignal signal));
        Assert.Equal("msg_1", signal.RefId);
        Assert.Equal(json.Length, signal.Size);
        Assert.Null(signal.Name);
    }
}
