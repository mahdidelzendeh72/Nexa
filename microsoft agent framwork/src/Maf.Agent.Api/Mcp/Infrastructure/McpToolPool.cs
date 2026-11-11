using System.Collections.Concurrent;
using Maf.Agent.Api.Mcp.Application;
using Maf.Agent.Api.Mcp.Domain;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Mcp.Infrastructure;

public sealed class McpToolPool(
    IMcpServerSessionFactory sessions,
    ILogger<McpToolPool> logger) : IMcpToolPool, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, PooledServer> _servers = new();
    private readonly CancellationTokenSource _stopping = new();
    private int _disposed;

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        await _stopping.CancelAsync();
        await _gate.WaitAsync();
        try
        {
            foreach (PooledServer server in _servers.Values)
            {
                await server.DisposeAsync();
            }

            _servers.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<AIFunction>> GetAsync(
        McpServerConnection connection,
        IReadOnlySet<string> names,
        CancellationToken cancellationToken)
    {
        if (_stopping.IsCancellationRequested)
        {
            return [];
        }

        string fingerprint = Fingerprint(connection);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_servers.TryGetValue(connection.ServerId, out PooledServer? current) &&
                current.Fingerprint == fingerprint)
            {
                return current.Match(names);
            }
        }
        finally
        {
            _gate.Release();
        }

        IMcpServerSession session;
        try
        {
            using CancellationTokenSource connect = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _stopping.Token);
            session = await sessions.ConnectAsync(connection, connect.Token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "ابزارهای سرور MCP برای این گفتگو باز نشد.");
            return [];
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_stopping.IsCancellationRequested)
            {
                await session.DisposeAsync();
                return [];
            }

            if (_servers.TryGetValue(connection.ServerId, out PooledServer? current) &&
                current.Fingerprint == fingerprint)
            {
                await session.DisposeAsync();
                return current.Match(names);
            }

            if (_servers.TryRemove(connection.ServerId, out PooledServer? previous))
            {
                await previous.DisposeAsync();
            }

            PooledServer pooled = new(fingerprint, session);
            _servers[connection.ServerId] = pooled;
            return pooled.Match(names);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask ReleaseAsync(Guid serverId)
    {
        await _gate.WaitAsync();
        try
        {
            if (_servers.TryRemove(serverId, out PooledServer? pooled))
            {
                await pooled.DisposeAsync();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string Fingerprint(McpServerConnection connection)
    {
        string transport = connection.Transport switch
        {
            McpServerTransport.HttpStreamable http =>
                $"http\n{http.Endpoint.AbsoluteUri}\n{http.HeadersJson}",
            McpServerTransport.Stdio stdio =>
                $"stdio\n{stdio.Command}\n{stdio.ArgumentsJson}\n{stdio.EnvironmentJson}",
            _ => "unknown",
        };
        return $"{connection.TimeoutSeconds}\n{transport}";
    }

    private sealed class PooledServer(string fingerprint, IMcpServerSession session) : IAsyncDisposable
    {
        private readonly IReadOnlyList<AIFunction> _functions = session.Functions;

        public string Fingerprint { get; } = fingerprint;

        public IReadOnlyList<AIFunction> Match(IReadOnlySet<string> names)
        {
            List<AIFunction> found = [];
            foreach (AIFunction function in _functions)
            {
                if (names.Contains(function.Name))
                {
                    found.Add(function);
                }
            }

            return found;
        }

        public ValueTask DisposeAsync() => session.DisposeAsync();
    }
}
