namespace Maf.Client.Mcp;

public sealed record McpServerSummary(
    Guid Id,
    string Name,
    string? Description,
    string Transport,
    string? Endpoint,
    string? Command,
    int TimeoutSeconds,
    bool IsEnabled,
    DateTimeOffset? LastDiscoveredAt,
    string? DiscoveryError,
    int ToolCount);

public sealed record McpServerDetails(
    Guid Id,
    string Name,
    string? Description,
    string Transport,
    string? Endpoint,
    string? HeadersJson,
    string? Command,
    string? ArgumentsJson,
    string? EnvironmentJson,
    int TimeoutSeconds,
    bool IsEnabled,
    DateTimeOffset? LastDiscoveredAt,
    string? DiscoveryError,
    McpToolDetails[] Tools);

public sealed record McpToolDetails(
    string Name,
    string? Title,
    string? Description,
    bool RequiresApproval);

public sealed record McpToolApprovalRequest(string Name, bool RequiresApproval);

public sealed record SaveMcpServerRequest(
    string Name,
    string? Description,
    string Transport,
    string? Endpoint,
    string? HeadersJson,
    string? Command,
    string? ArgumentsJson,
    string? EnvironmentJson,
    int TimeoutSeconds,
    bool IsEnabled);

