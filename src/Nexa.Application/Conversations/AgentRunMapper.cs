using Nexa.Contracts.Runtime;
using Nexa.Domain.AgentRuntime;

namespace Nexa.Application.Conversations;

internal static class AgentRunMapper
{
    public static AgentRunDto ToDto(AgentRun run) =>
        new(
            run.Id,
            run.ConversationId,
            run.AgentVersionId,
            run.Status.ToString(),
            run.CorrelationId,
            run.StartedAt,
            run.CompletedAt,
            run.InputTokens,
            run.OutputTokens,
            run.TotalTokens,
            run.LatencyMs,
            run.ErrorCode,
            run.ErrorMessage,
            run.ToolExecutions.Select(ToDto).ToList());

    private static ToolExecutionDto ToDto(ToolExecution execution) =>
        new(
            execution.Id,
            execution.ToolName,
            execution.CallId,
            execution.Arguments,
            execution.Result,
            execution.StartedAt,
            execution.CompletedAt);
}
