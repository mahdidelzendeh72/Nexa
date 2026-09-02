using Nexa.Domain.Models;

namespace Nexa.Application.Abstractions;

public sealed record AgentRuntimeMessage(string Role, string Content);

public sealed record AgentRuntimeRequest(
    string AgentName,
    string Instructions,
    IReadOnlyList<AgentRuntimeMessage> History,
    ModelProvider Provider,
    ModelProfile Profile,
    string? SessionState,
    IReadOnlyList<string>? ToolIds,
    int? MaxOutputTokens);

public enum AgentRuntimeEventKind
{
    RunStarted,
    TextDelta,
    ToolCallStarted,
    ToolCallCompleted,
    RunCompleted,
    Error
}

public sealed record AgentRuntimeEvent(
    AgentRuntimeEventKind Kind,
    string? Text = null,
    string? ToolName = null,
    string? ToolCallId = null,
    string? ToolArguments = null,
    string? ToolResult = null,
    int? InputTokens = null,
    int? OutputTokens = null,
    int? TotalTokens = null,
    int? LatencyMs = null,
    string? SessionState = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public interface IAgentRuntime
{
    IAsyncEnumerable<AgentRuntimeEvent> RunStreamingAsync(AgentRuntimeRequest request, CancellationToken cancellationToken);
}
