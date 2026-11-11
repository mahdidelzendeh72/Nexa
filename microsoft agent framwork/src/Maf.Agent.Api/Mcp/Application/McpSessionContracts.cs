using Maf.Agent.Api.Mcp.Domain;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Mcp.Application;

public sealed record McpServerConnection(
    Guid ServerId,
    string Name,
    int TimeoutSeconds,
    McpServerTransport Transport);

public sealed class McpDiscoveryException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public interface IMcpServerSession : IAsyncDisposable
{
    IReadOnlyList<DiscoveredMcpTool> Tools { get; }

    IReadOnlyList<AIFunction> Functions { get; }
}

public interface IMcpServerSessionFactory
{
    Task<IMcpServerSession> ConnectAsync(
        McpServerConnection connection,
        CancellationToken cancellationToken);
}
