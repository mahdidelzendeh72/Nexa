using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;
using ollama.Configuration;

namespace ollama;

public sealed class McpServerManager : IAsyncDisposable
{
    private readonly List<McpServerConnection> _connections = [];

    public IReadOnlyList<DiscoveredTool> DiscoveredTools { get; private set; } = [];

    public async Task ConnectAsync(IEnumerable<McpServerSettings> servers, CancellationToken cancellationToken = default)
    {
        var discovered = new List<DiscoveredTool>();

        foreach (var server in servers)
        {
            try
            {
                var transport = CreateTransport(server);
                var client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
                var tools = await client.ListToolsAsync(cancellationToken: cancellationToken);

                _connections.Add(new McpServerConnection(server.Name, client, tools));

                foreach (var tool in tools)
                {
                    discovered.Add(new DiscoveredTool(server.Name, tool.Name, tool.Description));
                }

                Console.WriteLine($"Connected to MCP server '{server.Name}' ({tools.Count} tools).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect to MCP server '{server.Name}': {ex.Message}");
            }
        }

        DiscoveredTools = discovered;
    }

    public int RegisterTools(Kernel kernel)
    {
        var registered = 0;

        foreach (var connection in _connections)
        {
            if (connection.Tools.Count == 0)
            {
                continue;
            }

            var pluginName = SanitizePluginName(connection.Name);
            kernel.Plugins.AddFromFunctions(
                pluginName,
                connection.Tools.Select(tool => tool.AsKernelFunction()));

            registered += connection.Tools.Count;
            Console.WriteLine($"Registered {connection.Tools.Count} tools from '{connection.Name}' as plugin '{pluginName}'.");
        }

        return registered;
    }

    public void PrintDiscoveredTools()
    {
        if (DiscoveredTools.Count == 0)
        {
            Console.WriteLine("No MCP tools discovered.");
            return;
        }

        Console.WriteLine("\nAvailable MCP tools:");
        foreach (var tool in DiscoveredTools)
        {
            var description = string.IsNullOrWhiteSpace(tool.Description) ? "(no description)" : tool.Description;
            Console.WriteLine($"  [{tool.ServerName}] {tool.ToolName}: {description}");
        }
        Console.WriteLine();
    }

    private static IClientTransport CreateTransport(McpServerSettings server)
    {
        return server.Transport.ToLowerInvariant() switch
        {
            "http" or "sse" => new HttpClientTransport(new HttpClientTransportOptions
            {
                Name = server.Name,
                Endpoint = new Uri(server.Endpoint ?? throw new InvalidOperationException(
                    $"MCP server '{server.Name}' requires an Endpoint.")),
                TransportMode = ParseTransportMode(server.TransportMode)
            }),
            "stdio" => new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = server.Name,
                Command = server.Command ?? throw new InvalidOperationException(
                    $"MCP server '{server.Name}' requires a Command."),
                Arguments = server.Arguments
            }),
            _ => throw new NotSupportedException(
                $"Transport '{server.Transport}' is not supported. Use Http or Stdio.")
        };
    }

    private static HttpTransportMode ParseTransportMode(string mode) =>
        mode.ToLowerInvariant() switch
        {
            "streamablehttp" => HttpTransportMode.StreamableHttp,
            "sse" => HttpTransportMode.Sse,
            "autodetect" or "auto" => HttpTransportMode.AutoDetect,
            _ => throw new NotSupportedException(
                $"HTTP transport mode '{mode}' is not supported. Use StreamableHttp, Sse, or AutoDetect.")
        };

    private static string SanitizePluginName(string name) =>
        string.IsNullOrWhiteSpace(name) ? "McpServer" : name.Replace(' ', '_');

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.Client.DisposeAsync();
        }

        _connections.Clear();
    }

    private sealed record McpServerConnection(
        string Name,
        McpClient Client,
        IList<McpClientTool> Tools);
}
