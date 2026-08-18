using Nexa.Contracts.Agents;
using Nexa.Domain.Agents;

namespace Nexa.Application.Agents;

internal static class AgentMapper
{
    public static AgentDto ToDto(Agent agent)
    {
        var current = agent.CurrentVersion;
        return new AgentDto(
            agent.Id,
            agent.Name,
            agent.Description,
            agent.OwnerUserId,
            agent.IsArchived,
            current.VersionNumber,
            current.Id,
            current.ModelProfileId,
            current.Instructions,
            current.MaxDurationSeconds,
            current.MaxToolCalls,
            current.TokenBudget,
            agent.CreatedAt,
            agent.UpdatedAt);
    }
}
