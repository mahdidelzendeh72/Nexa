using Maf.Agent.Api.Mcp.Application;
using Maf.Agent.Api.Mcp.Domain;
using Maf.Agent.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Mcp.Infrastructure;

public sealed class PostgresMcpServerStore(ApplicationDbContext dbContext) : IMcpServerStore
{
    public async Task<IReadOnlyList<McpServer>> ListAsync(
        CancellationToken cancellationToken) =>
        await dbContext.McpServers
            .Include(server => server.Tools)
            .Include(server => server.ToolApprovals)
            .OrderBy(server => server.Name)
            .ToListAsync(cancellationToken);

    public Task<McpServer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.McpServers
            .Include(server => server.Tools)
            .Include(server => server.ToolApprovals)
            .SingleOrDefaultAsync(server => server.Id == id, cancellationToken);

    public async Task AddAsync(McpServer server, CancellationToken cancellationToken)
    {
        dbContext.McpServers.Add(server);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task DeleteAsync(McpServer server, CancellationToken cancellationToken)
    {
        dbContext.McpServers.Remove(server);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ThrowIfNameTakenAsync(
        Guid serverId,
        string normalizedName,
        CancellationToken cancellationToken)
    {
        bool taken = await dbContext.McpServers.AnyAsync(
            server => server.Id != serverId && server.NormalizedName == normalizedName,
            cancellationToken);
        if (taken)
        {
            throw new McpServerNameConflictException(normalizedName);
        }
    }
}
