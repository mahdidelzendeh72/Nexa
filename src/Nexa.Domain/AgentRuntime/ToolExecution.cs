using Nexa.Domain.Common;

namespace Nexa.Domain.AgentRuntime;

public sealed class ToolExecution
{
    private ToolExecution()
    {
    }

    public Guid Id { get; private set; }
    public Guid AgentRunId { get; private set; }
    public string ToolName { get; private set; } = null!;
    public string? CallId { get; private set; }
    public string? Arguments { get; private set; }
    public string? Result { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    internal static ToolExecution Start(Guid agentRunId, string toolName, string? callId, string? arguments, DateTimeOffset startedAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            AgentRunId = Guard.NotEmpty(agentRunId, nameof(agentRunId)),
            ToolName = Guard.NotNullOrWhiteSpace(toolName, nameof(toolName), 200),
            CallId = callId,
            Arguments = arguments,
            StartedAt = startedAt
        };

    public void Complete(string? result, DateTimeOffset completedAt)
    {
        Result = result;
        CompletedAt = completedAt;
    }
}
