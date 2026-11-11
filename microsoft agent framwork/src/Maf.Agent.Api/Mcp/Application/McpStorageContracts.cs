using Maf.Agent.Api.Mcp.Domain;

namespace Maf.Agent.Api.Mcp.Application;

public sealed record SaveMcpServer(
    string Name,
    string? Description,
    McpTransportKind Transport,
    string? Endpoint,
    string? HeadersJson,
    string? Command,
    string? ArgumentsJson,
    string? EnvironmentJson,
    int TimeoutSeconds,
    bool IsEnabled);

public sealed class McpServerNameConflictException(string name)
    : Exception($"سروری با نام «{name}» قبلاً ثبت شده است.")
{
    public string Name { get; } = name;
}

public interface IMcpServerStore
{
    Task<IReadOnlyList<McpServer>> ListAsync(CancellationToken cancellationToken);

    Task<McpServer?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(McpServer server, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    Task DeleteAsync(McpServer server, CancellationToken cancellationToken);

    Task ThrowIfNameTakenAsync(
        Guid serverId,
        string normalizedName,
        CancellationToken cancellationToken);
}
