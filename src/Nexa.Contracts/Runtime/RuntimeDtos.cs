namespace Nexa.Contracts.Runtime;

public sealed record ToolDescriptorDto(
    string Id,
    string Name,
    string Description,
    string Category,
    string Version,
    bool IsEnabled,
    bool RequiresApproval,
    string SecurityLevel);

public sealed record AgentRunDto(
    Guid Id,
    Guid ConversationId,
    Guid AgentVersionId,
    string Status,
    string CorrelationId,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens,
    int? LatencyMs,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<ToolExecutionDto> ToolExecutions);

public sealed record ToolExecutionDto(
    Guid Id,
    string ToolName,
    string? CallId,
    string? Arguments,
    string? Result,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt);

public sealed record AgentStreamEventDto(
    string Kind,
    Guid? RunId = null,
    string? Text = null,
    string? ToolName = null,
    string? ToolCallId = null,
    string? ToolArguments = null,
    string? ToolResult = null,
    int? InputTokens = null,
    int? OutputTokens = null,
    int? TotalTokens = null,
    int? LatencyMs = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
