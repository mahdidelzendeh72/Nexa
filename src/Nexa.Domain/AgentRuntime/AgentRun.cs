using Nexa.Domain.Common;

namespace Nexa.Domain.AgentRuntime;

public sealed class AgentRun
{
    private readonly List<ToolExecution> _toolExecutions = [];

    private AgentRun()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid AgentVersionId { get; private set; }
    public Guid UserId { get; private set; }
    public AgentRunStatus Status { get; private set; }
    public string CorrelationId { get; private set; } = null!;
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public int? TotalTokens { get; private set; }
    public int? LatencyMs { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }

    public IReadOnlyCollection<ToolExecution> ToolExecutions => _toolExecutions;

    public static AgentRun Start(
        Guid conversationId,
        Guid agentVersionId,
        Guid userId,
        string correlationId)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentRun
        {
            Id = Guid.CreateVersion7(),
            ConversationId = Guard.NotEmpty(conversationId, nameof(conversationId)),
            AgentVersionId = Guard.NotEmpty(agentVersionId, nameof(agentVersionId)),
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            Status = AgentRunStatus.Running,
            CorrelationId = Guard.NotNullOrWhiteSpace(correlationId, nameof(correlationId), 200),
            StartedAt = now
        };
    }

    public ToolExecution RecordToolStart(string toolName, string? callId, string? arguments)
    {
        var execution = ToolExecution.Start(Id, toolName, callId, arguments, DateTimeOffset.UtcNow);
        _toolExecutions.Add(execution);
        return execution;
    }

    public void Complete(int? inputTokens, int? outputTokens, int? totalTokens, int latencyMs)
    {
        EnsureRunning();
        Status = AgentRunStatus.Completed;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        TotalTokens = totalTokens;
        LatencyMs = latencyMs;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Fail(string errorCode, string errorMessage, int latencyMs)
    {
        EnsureRunning();
        Status = AgentRunStatus.Failed;
        ErrorCode = Guard.NotNullOrWhiteSpace(errorCode, nameof(errorCode), 80);
        ErrorMessage = Guard.NotNullOrWhiteSpace(errorMessage, nameof(errorMessage), 2000);
        LatencyMs = latencyMs;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel(int latencyMs)
    {
        EnsureRunning();
        Status = AgentRunStatus.Cancelled;
        LatencyMs = latencyMs;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureRunning()
    {
        if (Status != AgentRunStatus.Running)
        {
            throw new InvalidOperationException("Only a running agent run can change status.");
        }
    }
}
