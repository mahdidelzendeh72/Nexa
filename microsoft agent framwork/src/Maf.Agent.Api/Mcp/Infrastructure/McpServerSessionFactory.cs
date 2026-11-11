using Maf.Agent.Api.Mcp.Application;
using Maf.Agent.Api.Mcp.Domain;
using ModelContextProtocol.Client;

namespace Maf.Agent.Api.Mcp.Infrastructure;

public sealed class McpServerSessionFactory : IMcpServerSessionFactory
{
    public Task<IMcpServerSession> ConnectAsync(
        McpServerConnection connection,
        CancellationToken cancellationToken)
    {
        IClientTransport transport = connection.Transport switch
        {
            McpServerTransport.HttpStreamable http => CreateHttpTransport(connection, http),
            McpServerTransport.Stdio stdio => CreateStdioTransport(connection, stdio),
            _ => throw new ArgumentException("نوع انتقال پشتیبانی نمی‌شود."),
        };
        return McpServerSession.ConnectAsync(connection, transport, cancellationToken);
    }

    private static HttpClientTransport CreateHttpTransport(
        McpServerConnection connection,
        McpServerTransport.HttpStreamable http)
    {
        HttpClient httpClient = new()
        {
            Timeout = Timeout.InfiniteTimeSpan,
        };
        return new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = http.Endpoint,
                Name = connection.Name,
                TransportMode = HttpTransportMode.StreamableHttp,
                ConnectionTimeout = TimeSpan.FromSeconds(connection.TimeoutSeconds),
                AdditionalHeaders = http.Headers.Count == 0
                    ? null
                    : new Dictionary<string, string>(http.Headers),
            },
            httpClient,
            ownsHttpClient: true);
    }

    private static StdioClientTransport CreateStdioTransport(
        McpServerConnection connection,
        McpServerTransport.Stdio stdio)
    {
        // Start from the SDK baseline instead of the API process environment, so
        // credentials already present in this process are not passed to the server.
        Dictionary<string, string?> environment = new(
            StdioClientTransportOptions.GetDefaultEnvironmentVariables());
        foreach ((string name, string value) in stdio.Environment)
        {
            environment[name] = value;
        }

        return new StdioClientTransport(
            new StdioClientTransportOptions
            {
                Name = connection.Name,
                Command = stdio.Command,
                Arguments = stdio.Arguments.ToList(),
                InheritEnvironmentVariables = false,
                EnvironmentVariables = environment,
                ShutdownTimeout = TimeSpan.FromSeconds(connection.TimeoutSeconds),
            });
    }
}
