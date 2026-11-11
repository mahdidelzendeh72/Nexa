using Maf.Agent.Api.Mcp.Application;
using Maf.Agent.Api.Mcp.Domain;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Tests;

public sealed class McpServerRegistryTests
{
    [Fact]
    public async Task Register_stores_the_server_and_the_tools_discovered_from_it()
    {
        FakeStore store = new();
        McpServerRegistry registry = new(
            store,
            new FakeSessionFactory(
            [
                new DiscoveredMcpTool("microsoft_docs_search", "Microsoft Docs Search", "Search docs"),
                new DiscoveredMcpTool("microsoft_docs_fetch", "Microsoft Docs Fetch", null),
            ]));

        McpServer server = await registry.RegisterAsync(HttpCommand(), CancellationToken.None);

        Assert.Equal(2, server.Tools.Count);
        Assert.Null(server.DiscoveryError);
        Assert.Equal(server.Id, Assert.Single(store.Servers).Id);
    }

    [Fact]
    public async Task Register_keeps_the_server_when_discovery_fails()
    {
        FakeStore store = new();
        McpServerRegistry registry = new(
            store,
            new ThrowingSessionFactory());

        McpServer server = await registry.RegisterAsync(HttpCommand(), CancellationToken.None);

        Assert.Empty(server.Tools);
        Assert.Contains("ناموفق", server.DiscoveryError, StringComparison.Ordinal);
        Assert.Single(store.Servers);
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_name_before_connecting()
    {
        FakeStore store = new();
        CountingSessionFactory sessions = new();
        McpServerRegistry registry = new(store, sessions);
        SaveMcpServer command = HttpCommand();
        await registry.RegisterAsync(command, CancellationToken.None);

        await Assert.ThrowsAsync<McpServerNameConflictException>(() =>
            registry.RegisterAsync(command, CancellationToken.None));

        Assert.Single(store.Servers);
        Assert.Equal(1, sessions.Connections);
    }

    [Fact]
    public async Task Saving_tool_approval_does_not_release_the_pooled_connection()
    {
        FakeStore store = new();
        CountingPool pool = new();
        McpServerRegistry registry = new(
            store,
            new FakeSessionFactory([new DiscoveredMcpTool("search", null, null)]),
            pool);

        McpServer server = await registry.RegisterAsync(HttpCommand(), CancellationToken.None);
        McpServer? updated = await registry.SetToolApprovalsAsync(
            server.Id,
            [new McpToolApprovalSetting("search", true)],
            CancellationToken.None);

        Assert.NotNull(updated);
        Assert.True(updated.RequiresApproval("search"));
        Assert.Equal(0, pool.Releases);
    }

    private static SaveMcpServer HttpCommand() =>
        new(
            "microsoft learning mcp",
            "connect to microsoft learning mcp",
            McpTransportKind.HttpStreamable,
            "https://learn.microsoft.com/api/mcp",
            """{"Authorization":"Bearer token"}""",
            null,
            null,
            null,
            30,
            true);

    private sealed class FakeStore : IMcpServerStore
    {
        public List<McpServer> Servers { get; } = [];

        public Task<IReadOnlyList<McpServer>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<McpServer>>(Servers);

        public Task<McpServer?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Servers.SingleOrDefault(server => server.Id == id));

        public Task AddAsync(McpServer server, CancellationToken cancellationToken)
        {
            Servers.Add(server);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(McpServer server, CancellationToken cancellationToken)
        {
            Servers.Remove(server);
            return Task.CompletedTask;
        }

        public Task ThrowIfNameTakenAsync(
            Guid serverId,
            string normalizedName,
            CancellationToken cancellationToken)
        {
            if (Servers.Any(server => server.Id != serverId && server.NormalizedName == normalizedName))
            {
                throw new McpServerNameConflictException(normalizedName);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CountingPool : IMcpToolPool
    {
        public int Releases { get; private set; }

        public Task<IReadOnlyList<AIFunction>> GetAsync(
            McpServerConnection connection,
            IReadOnlySet<string> names,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AIFunction>>([]);

        public ValueTask ReleaseAsync(Guid serverId)
        {
            Releases++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeSessionFactory(IReadOnlyList<DiscoveredMcpTool> tools)
        : IMcpServerSessionFactory
    {
        public Task<IMcpServerSession> ConnectAsync(
            McpServerConnection connection,
            CancellationToken cancellationToken) =>
            Task.FromResult<IMcpServerSession>(new FakeSession(tools));
    }

    private sealed class CountingSessionFactory : IMcpServerSessionFactory
    {
        public int Connections { get; private set; }

        public Task<IMcpServerSession> ConnectAsync(
            McpServerConnection connection,
            CancellationToken cancellationToken)
        {
            Connections++;
            return Task.FromResult<IMcpServerSession>(new FakeSession([]));
        }
    }

    private sealed class ThrowingSessionFactory : IMcpServerSessionFactory
    {
        public Task<IMcpServerSession> ConnectAsync(
            McpServerConnection connection,
            CancellationToken cancellationToken) =>
            throw new McpDiscoveryException("شناسایی ابزارها ناموفق بود.");
    }

    private sealed class FakeSession(IReadOnlyList<DiscoveredMcpTool> tools) : IMcpServerSession
    {
        public IReadOnlyList<DiscoveredMcpTool> Tools { get; } = tools;

        public IReadOnlyList<AIFunction> Functions { get; } = [];

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
