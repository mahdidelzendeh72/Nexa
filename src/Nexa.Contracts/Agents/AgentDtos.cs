namespace Nexa.Contracts.Agents;

public sealed record AgentDto(
    Guid Id,
    string Name,
    string Description,
    Guid OwnerUserId,
    bool IsArchived,
    int CurrentVersionNumber,
    Guid CurrentVersionId,
    Guid ModelProfileId,
    string Instructions,
    int? MaxDurationSeconds,
    int? MaxToolCalls,
    int? TokenBudget,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AgentVersionDto(
    Guid Id,
    int VersionNumber,
    Guid ModelProfileId,
    string Instructions,
    DateTimeOffset CreatedAt);

public sealed record CreateAgentRequest(
    string Name,
    string? Description,
    string Instructions,
    Guid ModelProfileId,
    int? MaxDurationSeconds,
    int? MaxToolCalls,
    int? TokenBudget);

public sealed record UpdateAgentRequest(
    string Name,
    string? Description);

public sealed record PublishAgentVersionRequest(
    string Instructions,
    Guid ModelProfileId,
    int? MaxDurationSeconds,
    int? MaxToolCalls,
    int? TokenBudget);
