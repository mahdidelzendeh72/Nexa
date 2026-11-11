using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Mcp.Application;

public interface IMcpToolPool
{
    Task<IReadOnlyList<AIFunction>> GetAsync(
        McpServerConnection connection,
        IReadOnlySet<string> names,
        CancellationToken cancellationToken);

    ValueTask ReleaseAsync(Guid serverId);
}
