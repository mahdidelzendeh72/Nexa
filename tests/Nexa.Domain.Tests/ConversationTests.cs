using FluentAssertions;
using Nexa.Domain.AgentRuntime;
using Nexa.Domain.Conversations;

namespace Nexa.Domain.Tests;

public sealed class ConversationTests
{
    [Fact]
    public void First_user_message_becomes_title()
    {
        var conversation = Conversation.Create(Guid.CreateVersion7(), Guid.CreateVersion7());

        conversation.AddMessage(MessageRole.User, "How do I cancel a booking?");

        conversation.Title.Should().Be("How do I cancel a booking?");
    }

    [Fact]
    public void EnsureOwnedBy_rejects_other_users()
    {
        var owner = Guid.CreateVersion7();
        var conversation = Conversation.Create(owner, Guid.CreateVersion7());

        var act = () => conversation.EnsureOwnedBy(Guid.CreateVersion7());

        act.Should().Throw<UnauthorizedAccessException>();
        conversation.Invoking(c => c.EnsureOwnedBy(owner)).Should().NotThrow();
    }

    [Fact]
    public void Runtime_session_state_is_stored_on_the_conversation()
    {
        var conversation = Conversation.Create(Guid.CreateVersion7(), Guid.CreateVersion7());
        conversation.SetRuntimeSessionState("""{"session":1}""");
        conversation.RuntimeSessionState.Should().Be("""{"session":1}""");
        conversation.SetRuntimeSessionState("  ");
        conversation.RuntimeSessionState.Should().BeNull();
    }
}

public sealed class AgentRunTests
{
    [Fact]
    public void Complete_records_usage_and_latency()
    {
        var run = AgentRun.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "corr");
        var tool = run.RecordToolStart("utc_now", "call-1", "{}");
        tool.Complete("2026-08-18", DateTimeOffset.UtcNow);

        run.Complete(11, 4, 15, 42);

        run.Status.Should().Be(AgentRunStatus.Completed);
        run.TotalTokens.Should().Be(15);
        run.LatencyMs.Should().Be(42);
        run.ToolExecutions.Should().ContainSingle(t => t.Result == "2026-08-18");
    }

    [Fact]
    public void Complete_rejects_a_run_that_already_failed()
    {
        var run = AgentRun.Start(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "corr");
        run.Fail("CHAT_FAILED", "boom", 9);

        var act = () => run.Complete(1, 1, 2, 10);

        act.Should().Throw<InvalidOperationException>();
    }
}
