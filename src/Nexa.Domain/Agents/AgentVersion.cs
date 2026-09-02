using Nexa.Domain.Common;

namespace Nexa.Domain.Agents;

public sealed class AgentVersion
{
    private AgentVersion()
    {
    }

    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public int VersionNumber { get; private set; }
    public string Instructions { get; private set; } = null!;
    public Guid ModelProfileId { get; private set; }
    public int? MaxDurationSeconds { get; private set; }
    public int? MaxToolCalls { get; private set; }
    public int? TokenBudget { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static AgentVersion Create(
        Guid agentId,
        int versionNumber,
        string instructions,
        Guid modelProfileId,
        ChatLimits limits,
        DateTimeOffset createdAt)
    {
        if (versionNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber));
        }

        return new AgentVersion
        {
            Id = Guid.CreateVersion7(),
            AgentId = Guard.NotEmpty(agentId, nameof(agentId)),
            VersionNumber = versionNumber,
            Instructions = Guard.NotNullOrWhiteSpace(instructions, nameof(instructions), 32_000),
            ModelProfileId = Guard.NotEmpty(modelProfileId, nameof(modelProfileId)),
            MaxDurationSeconds = limits.MaxDurationSeconds,
            MaxToolCalls = limits.MaxToolCalls,
            TokenBudget = limits.TokenBudget,
            CreatedAt = createdAt
        };
    }
}
