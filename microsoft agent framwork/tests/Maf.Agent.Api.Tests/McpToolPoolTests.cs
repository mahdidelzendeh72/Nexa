using Maf.Agent.Api.Mcp.Application;
using Maf.Agent.Api.Mcp.Domain;
using Maf.Agent.Api.Mcp.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Maf.Agent.Api.Tests;

public sealed class McpToolPoolTests
{
    [Fact]
    public async Task Get_connects_once_and_reuses_the_same_server()
    {
        CountingFactory factory = new(["search", "fetch"]);
        McpToolPool pool = Pool(factory);
        McpServerConnection connection = Connection(30);
        HashSet<string> names = new(StringComparer.Ordinal) { "search" };

        IReadOnlyList<AIFunction> first = await pool.GetAsync(connection, names, CancellationToken.None);
        IReadOnlyList<AIFunction> second = await pool.GetAsync(connection, names, CancellationToken.None);

        Assert.Equal("search", Assert.Single(first).Name);
        Assert.Equal("search", Assert.Single(second).Name);
        Assert.Equal(1, factory.Connections);
    }

    [Fact]
    public async Task Release_closes_the_connection_so_the_next_get_opens_another()
    {
        CountingFactory factory = new(["search"]);
        McpToolPool pool = Pool(factory);
        McpServerConnection connection = Connection(30);
        HashSet<string> names = new(StringComparer.Ordinal) { "search" };

        await pool.GetAsync(connection, names, CancellationToken.None);
        await pool.ReleaseAsync(connection.ServerId);
        await pool.GetAsync(connection, names, CancellationToken.None);

        Assert.Equal(2, factory.Connections);
        Assert.Equal(1, factory.Disposed);
    }

    [Fact]
    public async Task Changed_connection_replaces_the_pooled_server()
    {
        CountingFactory factory = new(["search"]);
        McpToolPool pool = Pool(factory);
        McpServerConnection connection = Connection(30);
        HashSet<string> names = new(StringComparer.Ordinal) { "search" };

        await pool.GetAsync(connection, names, CancellationToken.None);
        await pool.GetAsync(connection with { TimeoutSeconds = 10 }, names, CancellationToken.None);

        Assert.Equal(2, factory.Connections);
        Assert.Equal(1, factory.Disposed);
    }

    [Fact]
    public async Task Failed_connection_is_not_kept()
    {
        CountingFactory factory = new(["search"], failFirst: true);
        McpToolPool pool = Pool(factory);
        McpServerConnection connection = Connection(30);
        HashSet<string> names = new(StringComparer.Ordinal) { "search" };

        Assert.Empty(await pool.GetAsync(connection, names, CancellationToken.None));
        Assert.Equal("search", Assert.Single(await pool.GetAsync(connection, names, CancellationToken.None)).Name);
        Assert.Equal(2, factory.Connections);
    }

    [Fact]
    public async Task Dispose_closes_open_connections()
    {
        CountingFactory factory = new(["search"]);
        McpToolPool pool = Pool(factory);
        HashSet<string> names = new(StringComparer.Ordinal) { "search" };

        await pool.GetAsync(Connection(30), names, CancellationToken.None);
        await pool.DisposeAsync();
        await pool.DisposeAsync();

        Assert.Equal(1, factory.Disposed);
        Assert.Empty(await pool.GetAsync(Connection(30), names, CancellationToken.None));
        Assert.Equal(1, factory.Connections);
    }

    private static McpToolPool Pool(CountingFactory factory) =>
        new(factory, NullLogger<McpToolPool>.Instance);

    private static McpServerConnection Connection(int timeoutSeconds) =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "docs",
            timeoutSeconds,
            new McpServerTransport.HttpStreamable(new Uri("https://example.com/mcp"), null));

    private sealed class CountingFactory(string[] names, bool failFirst = false) : IMcpServerSessionFactory
    {
        private readonly List<FakeSession> _sessions = [];

        public int Connections { get; private set; }

        public int Disposed => _sessions.Count(session => session.Disposed);

        public Task<IMcpServerSession> ConnectAsync(
            McpServerConnection connection,
            CancellationToken cancellationToken)
        {
            Connections++;
            if (failFirst && Connections == 1)
            {
                throw new McpDiscoveryException("اتصال برقرار نشد.");
            }

            FakeSession session = new(names.Select(name => AIFunctionFactory.Create(() => "ok", name)).ToArray());
            _sessions.Add(session);
            return Task.FromResult<IMcpServerSession>(session);
        }
    }

    private sealed class FakeSession(IReadOnlyList<AIFunction> functions) : IMcpServerSession
    {
        public bool Disposed { get; private set; }

        public IReadOnlyList<DiscoveredMcpTool> Tools { get; } = [];

        public IReadOnlyList<AIFunction> Functions { get; } = functions;

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
