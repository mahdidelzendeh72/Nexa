using Maf.Agent.Api.Mcp.Domain;
using Maf.Agent.Api.Mcp.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Tests;

public sealed class McpToolPersistenceTests
{
    [Fact]
    public async Task Discovered_tools_are_inserted_with_the_server()
    {
        await using McpPersistenceContext context = await CreateContextAsync();
        McpServer server = CreateServer();
        context.Servers.Add(server);
        server.RecordDiscovery(
            [
                new DiscoveredMcpTool("microsoft_docs_search", "Microsoft Docs Search", "Search"),
                new DiscoveredMcpTool("microsoft_docs_fetch", "Microsoft Docs Fetch", "Fetch"),
            ],
            DateTimeOffset.UtcNow);

        await context.SaveChangesAsync();

        McpServer stored = await context.Servers
            .Include(item => item.Tools)
            .SingleAsync();
        Assert.Equal(2, stored.Tools.Count);
        Assert.All(stored.Tools, tool => Assert.NotEqual(0, tool.Id));
    }

    [Fact]
    public async Task Replacing_tools_deletes_the_previous_rows()
    {
        await using McpPersistenceContext context = await CreateContextAsync();
        McpServer server = CreateServer();
        context.Servers.Add(server);
        server.RecordDiscovery(
            [new DiscoveredMcpTool("microsoft_docs_search", null, null)],
            DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        server.RecordDiscovery(
            [new DiscoveredMcpTool("microsoft_code_sample_search", "Samples", null)],
            DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        McpServer stored = await context.Servers
            .Include(item => item.Tools)
            .SingleAsync();
        Assert.Equal("microsoft_code_sample_search", Assert.Single(stored.Tools).Name);
    }

    [Fact]
    public async Task Removing_a_discovered_tool_keeps_its_approval_row()
    {
        await using McpPersistenceContext context = await CreateContextAsync();
        McpServer server = CreateServer();
        context.Servers.Add(server);
        server.RecordDiscovery(
            [
                new DiscoveredMcpTool("search", null, null),
                new DiscoveredMcpTool("fetch", null, null),
            ],
            DateTimeOffset.UtcNow);
        server.SetToolApprovals([new McpToolApprovalSetting("search", true)]);
        await context.SaveChangesAsync();

        server.RecordDiscovery(
            [new DiscoveredMcpTool("fetch", null, null)],
            DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        McpServer stored = await context.Servers
            .Include(item => item.Tools)
            .Include(item => item.ToolApprovals)
            .SingleAsync();
        Assert.Equal("fetch", Assert.Single(stored.Tools).Name);
        Assert.True(stored.RequiresApproval("search"));
    }

    [Fact]
    public async Task Deleting_the_server_deletes_its_tool_approvals()
    {
        await using McpPersistenceContext context = await CreateContextAsync();
        McpServer server = CreateServer();
        context.Servers.Add(server);
        server.RecordDiscovery(
            [new DiscoveredMcpTool("search", null, null)],
            DateTimeOffset.UtcNow);
        server.SetToolApprovals([new McpToolApprovalSetting("search", true)]);
        await context.SaveChangesAsync();

        context.Servers.Remove(server);
        await context.SaveChangesAsync();

        Assert.Empty(await context.Set<McpToolApproval>().ToListAsync());
        Assert.Empty(await context.Set<McpDiscoveredTool>().ToListAsync());
    }

    private static async Task<McpPersistenceContext> CreateContextAsync()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        DbContextOptions<McpPersistenceContext> options =
            new DbContextOptionsBuilder<McpPersistenceContext>()
                .UseSqlite(connection)
                .Options;
        McpPersistenceContext context = new(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static McpServer CreateServer() =>
        McpServer.Register(
            Guid.NewGuid(),
            "microsoft learning mcp",
            null,
            new McpServerTransport.HttpStreamable(
                new Uri("https://learn.microsoft.com/api/mcp"),
                null),
            30,
            true,
            DateTimeOffset.UtcNow);

    private sealed class McpPersistenceContext(DbContextOptions<McpPersistenceContext> options)
        : DbContext(options)
    {
        public DbSet<McpServer> Servers => Set<McpServer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ConfigureMcpModel();
        }
    }
}
