using Maf.Agent.Api.Mcp.Domain;

namespace Maf.Agent.Api.Mcp.Application;

public sealed class McpServerRegistry(
    IMcpServerStore store,
    IMcpServerSessionFactory sessions,
    IMcpToolPool? pool = null)
{
    public Task<IReadOnlyList<McpServer>> ListAsync(CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);

    public Task<McpServer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        store.FindAsync(id, cancellationToken);

    public async Task<McpServer> RegisterAsync(
        SaveMcpServer command,
        CancellationToken cancellationToken)
    {
        McpServer server = McpServer.Register(
            Guid.NewGuid(),
            command.Name,
            command.Description,
            ParseTransport(command),
            command.TimeoutSeconds,
            command.IsEnabled,
            DateTimeOffset.UtcNow);
        await store.ThrowIfNameTakenAsync(
            server.Id,
            server.NormalizedName,
            cancellationToken);
        await store.AddAsync(server, cancellationToken);
        await DiscoverCoreAsync(server, cancellationToken);
        return server;
    }

    public async Task<McpServer?> UpdateAsync(
        Guid id,
        SaveMcpServer command,
        CancellationToken cancellationToken)
    {
        McpServer? server = await store.FindAsync(id, cancellationToken);
        if (server is null)
        {
            return null;
        }

        server.Update(
            command.Name,
            command.Description,
            ParseTransport(command),
            command.TimeoutSeconds,
            command.IsEnabled,
            DateTimeOffset.UtcNow);
        await store.ThrowIfNameTakenAsync(
            server.Id,
            server.NormalizedName,
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        await DiscoverCoreAsync(server, cancellationToken);
        await ReleasePoolAsync(server.Id);
        return server;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        McpServer? server = await store.FindAsync(id, cancellationToken);
        if (server is null)
        {
            return false;
        }

        await store.DeleteAsync(server, cancellationToken);
        await ReleasePoolAsync(server.Id);
        return true;
    }

    public async Task<McpServer?> SetToolApprovalsAsync(
        Guid id,
        IReadOnlyList<McpToolApprovalSetting> settings,
        CancellationToken cancellationToken)
    {
        McpServer? server = await store.FindAsync(id, cancellationToken);
        if (server is null)
        {
            return null;
        }

        server.SetToolApprovals(settings);
        await store.SaveChangesAsync(cancellationToken);
        return server;
    }

    public async Task<McpServer?> DiscoverAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        McpServer? server = await store.FindAsync(id, cancellationToken);
        if (server is null)
        {
            return null;
        }

        await DiscoverCoreAsync(server, cancellationToken);
        await ReleasePoolAsync(server.Id);
        return server;
    }

    private async Task ReleasePoolAsync(Guid serverId)
    {
        if (pool is not null)
        {
            await pool.ReleaseAsync(serverId);
        }
    }

    private async Task DiscoverCoreAsync(
        McpServer server,
        CancellationToken cancellationToken)
    {
        try
        {
            await using IMcpServerSession session = await sessions.ConnectAsync(
                new McpServerConnection(
                    server.Id,
                    server.Name,
                    server.TimeoutSeconds,
                    server.ToTransport()),
                cancellationToken);
            server.RecordDiscovery(session.Tools, DateTimeOffset.UtcNow);
        }
        catch (McpDiscoveryException exception)
        {
            server.RecordDiscoveryFailure(exception.Message, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception)
        {
            server.RecordDiscoveryFailure(exception.Message, DateTimeOffset.UtcNow);
        }

        await store.SaveChangesAsync(cancellationToken);
    }

    private static McpServerTransport ParseTransport(SaveMcpServer command) =>
        command.Transport switch
        {
            McpTransportKind.HttpStreamable => new McpServerTransport.HttpStreamable(
                ParseEndpoint(command.Endpoint),
                command.HeadersJson),
            McpTransportKind.Stdio => new McpServerTransport.Stdio(
                command.Command ?? "",
                command.ArgumentsJson,
                command.EnvironmentJson),
            _ => throw new ArgumentException("نوع انتقال باید HTTP جریان‌پذیر یا Stdio باشد."),
        };

    private static Uri ParseEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint) ||
            !Uri.TryCreate(endpoint.Trim(), UriKind.Absolute, out Uri? uri))
        {
            throw new ArgumentException("آدرس سرور باید یک URL مطلق http یا https باشد.");
        }

        return uri;
    }
}
