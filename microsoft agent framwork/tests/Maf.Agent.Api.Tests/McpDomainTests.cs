using Maf.Agent.Api.Mcp.Domain;

namespace Maf.Agent.Api.Tests;

public sealed class McpDomainTests
{
    [Fact]
    public void Register_rejects_a_non_http_endpoint()
    {
        Assert.Throws<ArgumentException>(() => Create(
            endpoint: new Uri("ftp://files.example/mcp")));
    }

    [Fact]
    public void Register_rejects_credentials_in_the_endpoint()
    {
        Assert.Throws<ArgumentException>(() => Create(
            endpoint: new Uri("https://user:secret@learn.microsoft.com/api/mcp")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(301)]
    public void Register_rejects_a_timeout_outside_the_supported_range(int timeoutSeconds)
    {
        Assert.Throws<ArgumentException>(() => Create(timeoutSeconds: timeoutSeconds));
    }

    [Fact]
    public void Http_headers_are_stored_as_a_json_object()
    {
        McpServer server = Create(headersJson: """{"Authorization":"Bearer token","X-Api-Key":"secret"}""");

        McpServerTransport.HttpStreamable transport =
            Assert.IsType<McpServerTransport.HttpStreamable>(server.ToTransport());
        Assert.Equal("Bearer token", transport.Headers["Authorization"]);
        Assert.Equal("secret", transport.Headers["X-Api-Key"]);
        Assert.Contains("\"Authorization\":\"Bearer token\"", server.HeadersJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Http_headers_reject_a_json_array()
    {
        Assert.Throws<ArgumentException>(() => Create(headersJson: "[\"Authorization\"]"));
    }

    [Fact]
    public void Stdio_command_arguments_and_environment_are_stored()
    {
        McpServer server = McpServer.Register(
            Guid.NewGuid(),
            "filesystem",
            null,
            new McpServerTransport.Stdio(
                " npx ",
                """["-y","@modelcontextprotocol/server-filesystem"]""",
                """{"API_KEY":"secret"}"""),
            30,
            true,
            DateTimeOffset.UtcNow);

        McpServerTransport.Stdio transport =
            Assert.IsType<McpServerTransport.Stdio>(server.ToTransport());
        Assert.Equal("npx", transport.Command);
        Assert.Equal(["-y", "@modelcontextprotocol/server-filesystem"], transport.Arguments);
        Assert.Equal("secret", transport.Environment["API_KEY"]);
        Assert.Null(server.Endpoint);
        Assert.Null(server.HeadersJson);
    }

    [Fact]
    public void Discovery_replaces_tools_and_clears_the_previous_error()
    {
        McpServer server = Create();
        server.RecordDiscoveryFailure("اتصال قطع شد.", DateTimeOffset.UtcNow);

        server.RecordDiscovery(
            [
                new DiscoveredMcpTool(
                    "microsoft_docs_search",
                    "Microsoft Docs Search",
                    "Search official docs"),
            ],
            DateTimeOffset.UtcNow);

        McpDiscoveredTool tool = Assert.Single(server.Tools);
        Assert.Equal("microsoft_docs_search", tool.Name);
        Assert.Equal("Microsoft Docs Search", tool.Title);
        Assert.Null(server.DiscoveryError);
        Assert.NotNull(server.LastDiscoveredAt);
    }

    [Fact]
    public void Failed_discovery_keeps_the_last_successful_tools()
    {
        McpServer server = Create();
        server.RecordDiscovery(
            [new DiscoveredMcpTool("microsoft_docs_fetch", "Microsoft Docs Fetch", null)],
            DateTimeOffset.UtcNow);

        server.RecordDiscoveryFailure("مهلت اتصال تمام شد.", DateTimeOffset.UtcNow);

        Assert.Equal("microsoft_docs_fetch", Assert.Single(server.Tools).Name);
        Assert.NotNull(server.LastDiscoveredAt);
        Assert.Equal("مهلت اتصال تمام شد.", server.DiscoveryError);
    }

    [Fact]
    public void Changing_headers_clears_previously_discovered_tools()
    {
        McpServer server = Create();
        server.RecordDiscovery(
            [new DiscoveredMcpTool("microsoft_docs_search", null, null)],
            DateTimeOffset.UtcNow);

        server.Update(
            server.Name,
            server.Description,
            new McpServerTransport.HttpStreamable(
                server.Endpoint!,
                """{"Authorization":"Bearer next"}"""),
            server.TimeoutSeconds,
            isEnabled: true,
            DateTimeOffset.UtcNow);

        Assert.Empty(server.Tools);
        Assert.Null(server.LastDiscoveredAt);
    }

    [Fact]
    public void Changing_the_endpoint_clears_previously_discovered_tools()
    {
        McpServer server = Create();
        server.RecordDiscovery(
            [new DiscoveredMcpTool("microsoft_docs_search", null, null)],
            DateTimeOffset.UtcNow);

        server.Update(
            server.Name,
            server.Description,
            new McpServerTransport.HttpStreamable(
                new Uri("https://example.test/mcp"),
                null),
            server.TimeoutSeconds,
            isEnabled: true,
            DateTimeOffset.UtcNow);

        Assert.Empty(server.Tools);
        Assert.Null(server.LastDiscoveredAt);
    }

    [Fact]
    public void Discovery_rejects_duplicate_tool_names_without_clearing_existing_tools()
    {
        McpServer server = Create();
        server.RecordDiscovery(
            [new DiscoveredMcpTool("microsoft_docs_search", null, null)],
            DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => server.RecordDiscovery(
            [
                new DiscoveredMcpTool("echo", null, null),
                new DiscoveredMcpTool("echo", null, null),
            ],
            DateTimeOffset.UtcNow));

        Assert.Equal("microsoft_docs_search", Assert.Single(server.Tools).Name);
    }

    [Fact]
    public void Tool_approval_stays_when_discovery_drops_that_tool()
    {
        McpServer server = Create();
        server.RecordDiscovery(
            [
                new DiscoveredMcpTool("search", null, null),
                new DiscoveredMcpTool("fetch", null, null),
            ],
            DateTimeOffset.UtcNow);
        server.SetToolApprovals(
        [
            new McpToolApprovalSetting("search", true),
            new McpToolApprovalSetting("fetch", false),
        ]);

        server.RecordDiscovery(
            [new DiscoveredMcpTool("fetch", null, null)],
            DateTimeOffset.UtcNow);

        Assert.True(server.RequiresApproval("search"));
        Assert.False(server.RequiresApproval("fetch"));
        Assert.Equal(2, server.ToolApprovals.Count);
        Assert.Equal("fetch", Assert.Single(server.Tools).Name);
    }

    [Fact]
    public void Approval_is_rejected_for_a_tool_that_is_not_discovered()
    {
        McpServer server = Create();
        server.RecordDiscovery(
            [new DiscoveredMcpTool("search", null, null)],
            DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => server.SetToolApprovals(
            [new McpToolApprovalSetting("missing", true)]));
        Assert.Empty(server.ToolApprovals);
    }

    private static McpServer Create(
        Uri? endpoint = null,
        string? headersJson = null,
        int timeoutSeconds = 30) =>
        McpServer.Register(
            Guid.NewGuid(),
            "microsoft learning mcp",
            "connect to microsoft learning mcp",
            new McpServerTransport.HttpStreamable(
                endpoint ?? new Uri("https://learn.microsoft.com/api/mcp"),
                headersJson),
            timeoutSeconds,
            isEnabled: true,
            DateTimeOffset.UtcNow);
}
