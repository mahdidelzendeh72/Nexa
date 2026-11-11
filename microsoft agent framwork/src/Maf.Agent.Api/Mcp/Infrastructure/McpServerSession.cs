using Maf.Agent.Api.Mcp.Application;
using Maf.Agent.Api.Mcp.Domain;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;

namespace Maf.Agent.Api.Mcp.Infrastructure;

public sealed class McpServerSession : IMcpServerSession
{
    private readonly McpClient _client;
    private readonly IClientTransport _transport;

    private McpServerSession(
        McpClient client,
        IClientTransport transport,
        IList<McpClientTool> tools)
    {
        _client = client;
        _transport = transport;
        Tools = tools
            .Select(tool => new DiscoveredMcpTool(
                tool.Name,
                tool.ProtocolTool.Title,
                string.IsNullOrWhiteSpace(tool.Description)
                    ? tool.ProtocolTool.Description
                    : tool.Description))
            .ToArray();
        Functions = tools.ToArray();
    }

    public IReadOnlyList<DiscoveredMcpTool> Tools { get; }

    public IReadOnlyList<AIFunction> Functions { get; }

    public static async Task<IMcpServerSession> ConnectAsync(
        McpServerConnection connection,
        IClientTransport transport,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(connection.TimeoutSeconds));

        McpClient? client = null;
        try
        {
            client = await McpClient.CreateAsync(
                transport,
                cancellationToken: timeout.Token);
            IList<McpClientTool> tools = await client.ListToolsAsync(
                cancellationToken: timeout.Token);
            McpServerSession session = new(client, transport, tools);
            client = null;
            return session;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await DisposeQuietlyAsync(client);
            await DisposeTransportAsync(transport);
            throw;
        }
        catch (OperationCanceledException)
        {
            await DisposeQuietlyAsync(client);
            await DisposeTransportAsync(transport);
            throw new McpDiscoveryException(
                $"اتصال به «{connection.Name}» در مهلت {connection.TimeoutSeconds} ثانیه برقرار نشد.");
        }
        catch (McpDiscoveryException)
        {
            await DisposeQuietlyAsync(client);
            await DisposeTransportAsync(transport);
            throw;
        }
        catch (Exception exception)
        {
            await DisposeQuietlyAsync(client);
            await DisposeTransportAsync(transport);
            throw new McpDiscoveryException(
                $"شناسایی ابزارهای «{connection.Name}» ناموفق بود: {exception.Message}",
                exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _client.DisposeAsync();
        }
        finally
        {
            await DisposeTransportAsync(_transport);
        }
    }

    private static Task DisposeTransportAsync(IClientTransport transport) =>
        transport is IAsyncDisposable disposable
            ? DisposeQuietlyAsync(disposable)
            : Task.CompletedTask;

    private static async Task DisposeQuietlyAsync(IAsyncDisposable? resource)
    {
        if (resource is null)
        {
            return;
        }

        try
        {
            await resource.DisposeAsync();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
        }
    }
}
