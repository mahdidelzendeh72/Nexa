using Maf.Agent.Api.Mcp.Application;
using Maf.Agent.Api.Mcp.Domain;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Chat.Application;

public sealed record ChatToolChoice(
    Guid ServerId,
    string ServerName,
    string Name,
    string? Title,
    string? Description);

public sealed class ChatToolCatalog(IServiceScopeFactory scopes, IMcpToolPool toolPool)
{
    public async Task<IReadOnlyList<ChatToolChoice>> ListAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IMcpServerStore store = scope.ServiceProvider.GetRequiredService<IMcpServerStore>();
        IReadOnlyList<McpServer> servers = await store.ListAsync(cancellationToken);
        return servers
            .Where(server => server.IsEnabled)
            .SelectMany(server => server.Tools
                .OrderBy(tool => tool.Name, StringComparer.Ordinal)
                .Select(tool => new ChatToolChoice(
                    server.Id,
                    server.Name,
                    tool.Name,
                    tool.Title,
                    tool.Description is { Length: > 300 } description ? description[..300] : tool.Description)))
            .ToArray();
    }

    public async Task<IReadOnlyList<AITool>> ResolveAsync(
        IReadOnlyList<ChatToolKey> keys,
        CancellationToken cancellationToken)
    {
        if (keys.Count == 0)
        {
            return [];
        }

        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IMcpServerStore store = scope.ServiceProvider.GetRequiredService<IMcpServerStore>();
        List<AITool> resolved = [];
        foreach (IGrouping<Guid, ChatToolKey> group in keys.GroupBy(key => key.ServerId))
        {
            McpServer? server = await store.FindAsync(group.Key, cancellationToken);
            if (server is not { IsEnabled: true })
            {
                continue;
            }

            HashSet<string> names = group
                .Select(key => key.Name)
                .Where(name => server.Tools.Any(tool => string.Equals(tool.Name, name, StringComparison.Ordinal)))
                .ToHashSet(StringComparer.Ordinal);
            if (names.Count == 0)
            {
                continue;
            }

            foreach (AIFunction function in await toolPool.GetAsync(
                new McpServerConnection(server.Id, server.Name, server.TimeoutSeconds, server.ToTransport()),
                names,
                cancellationToken))
            {
                resolved.Add(server.RequiresApproval(function.Name)
                    ? new ApprovalRequiredAIFunction(function)
                    : function);
            }
        }

        return resolved;
    }
}
