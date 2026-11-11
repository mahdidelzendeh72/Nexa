using Maf.Agent.Api.Mcp.Application;
using Maf.Agent.Api.Mcp.Domain;

namespace Maf.Agent.Api.Mcp.Endpoints;

public static class McpEndpoints
{
    public static IServiceCollection AddMcpApi(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(
                McpPolicies.Manage,
                policy => policy.RequireRole(McpRoles.Manager));
        return services;
    }

    public static IEndpointRouteBuilder MapMcpEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/mcp")
            .WithTags("Mcp")
            .RequireAuthorization(McpPolicies.Manage);

        group.MapGet("/servers", ListAsync);
        group.MapGet("/servers/{serverId:guid}", GetAsync);
        group.MapPost("/servers", CreateAsync);
        group.MapPut("/servers/{serverId:guid}", UpdateAsync);
        group.MapDelete("/servers/{serverId:guid}", DeleteAsync);
        group.MapPost("/servers/{serverId:guid}/discover", DiscoverAsync);
        group.MapPut("/servers/{serverId:guid}/tool-approvals", SetToolApprovalsAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        McpServerRegistry registry,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<McpServer> servers = await registry.ListAsync(cancellationToken);
        return Results.Ok(servers.Select(ToSummary).ToArray());
    }

    private static async Task<IResult> GetAsync(
        Guid serverId,
        McpServerRegistry registry,
        CancellationToken cancellationToken)
    {
        McpServer? server = await registry.FindAsync(serverId, cancellationToken);
        return server is null ? Results.NotFound() : Results.Ok(ToDetails(server));
    }

    private static Task<IResult> CreateAsync(
        SaveMcpServerRequest request,
        McpServerRegistry registry,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            McpServer server = await registry.RegisterAsync(
                ToCommand(request),
                cancellationToken);
            return Results.Created($"/api/mcp/servers/{server.Id}", ToDetails(server));
        });

    private static Task<IResult> UpdateAsync(
        Guid serverId,
        SaveMcpServerRequest request,
        McpServerRegistry registry,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            McpServer? server = await registry.UpdateAsync(
                serverId,
                ToCommand(request),
                cancellationToken);
            return server is null ? Results.NotFound() : Results.Ok(ToDetails(server));
        });

    private static async Task<IResult> DeleteAsync(
        Guid serverId,
        McpServerRegistry registry,
        CancellationToken cancellationToken)
    {
        bool deleted = await registry.DeleteAsync(serverId, cancellationToken);
        return deleted ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> DiscoverAsync(
        Guid serverId,
        McpServerRegistry registry,
        CancellationToken cancellationToken)
    {
        McpServer? server = await registry.DiscoverAsync(serverId, cancellationToken);
        return server is null ? Results.NotFound() : Results.Ok(ToDetails(server));
    }

    private static Task<IResult> SetToolApprovalsAsync(
        Guid serverId,
        McpToolApprovalRequest[] request,
        McpServerRegistry registry,
        CancellationToken cancellationToken) =>
        ExecuteAsync(async () =>
        {
            McpServer? server = await registry.SetToolApprovalsAsync(
                serverId,
                request.Select(item => new McpToolApprovalSetting(item.Name, item.RequiresApproval)).ToArray(),
                cancellationToken);
            return server is null ? Results.NotFound() : Results.Ok(ToDetails(server));
        });

    private static async Task<IResult> ExecuteAsync(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
        catch (McpServerNameConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
    }

    private static SaveMcpServer ToCommand(SaveMcpServerRequest request)
    {
        if (!Enum.TryParse(request.Transport, out McpTransportKind transport))
        {
            throw new ArgumentException("نوع انتقال باید HttpStreamable یا Stdio باشد.");
        }

        return new SaveMcpServer(
            request.Name,
            request.Description,
            transport,
            request.Endpoint,
            request.HeadersJson,
            request.Command,
            request.ArgumentsJson,
            request.EnvironmentJson,
            request.TimeoutSeconds,
            request.IsEnabled);
    }

    private static McpServerSummary ToSummary(McpServer server) =>
        new(
            server.Id,
            server.Name,
            server.Description,
            server.Transport.ToString(),
            server.Endpoint?.AbsoluteUri,
            server.Command,
            server.TimeoutSeconds,
            server.IsEnabled,
            server.LastDiscoveredAt,
            server.DiscoveryError,
            server.Tools.Count);

    private static McpServerDetails ToDetails(McpServer server) =>
        new(
            server.Id,
            server.Name,
            server.Description,
            server.Transport.ToString(),
            server.Endpoint?.AbsoluteUri,
            server.HeadersJson,
            server.Command,
            server.ArgumentsJson,
            server.EnvironmentJson,
            server.TimeoutSeconds,
            server.IsEnabled,
            server.LastDiscoveredAt,
            server.DiscoveryError,
            server.Tools
                .OrderBy(tool => tool.Name, StringComparer.Ordinal)
                .Select(tool => new McpToolResponse(
                    tool.Name,
                    tool.Title,
                    tool.Description,
                    server.RequiresApproval(tool.Name)))
                .ToArray());
}

public sealed record SaveMcpServerRequest(
    string Name,
    string? Description,
    string Transport,
    string? Endpoint,
    string? HeadersJson,
    string? Command,
    string? ArgumentsJson,
    string? EnvironmentJson,
    int TimeoutSeconds,
    bool IsEnabled);

public sealed record McpServerSummary(
    Guid Id,
    string Name,
    string? Description,
    string Transport,
    string? Endpoint,
    string? Command,
    int TimeoutSeconds,
    bool IsEnabled,
    DateTimeOffset? LastDiscoveredAt,
    string? DiscoveryError,
    int ToolCount);

public sealed record McpServerDetails(
    Guid Id,
    string Name,
    string? Description,
    string Transport,
    string? Endpoint,
    string? HeadersJson,
    string? Command,
    string? ArgumentsJson,
    string? EnvironmentJson,
    int TimeoutSeconds,
    bool IsEnabled,
    DateTimeOffset? LastDiscoveredAt,
    string? DiscoveryError,
    McpToolResponse[] Tools);

public sealed record McpToolApprovalRequest(string Name, bool RequiresApproval);

public sealed record McpToolResponse(
    string Name,
    string? Title,
    string? Description,
    bool RequiresApproval);
