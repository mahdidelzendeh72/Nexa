using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexa.Application.Agents;
using Nexa.Application.Common;
using Nexa.Contracts.Agents;

namespace Nexa.Api.Endpoints;

public static class AgentEndpoints
{
    public static RouteGroupBuilder MapAgentEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/agents").RequireAuthorization(NexaPolicies.CanView);

        group.MapGet("/", async (IAgentService service, bool includeArchived, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(includeArchived, cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, IAgentService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(id, cancellationToken)));

        group.MapGet("/{id:guid}/versions", async (Guid id, IAgentService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListVersionsAsync(id, cancellationToken)));

        group.MapPost("/", async (CreateAgentRequest request, IAgentService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/agents/{created.Id}", created);
            })
            .RequireAuthorization(NexaPolicies.CanManageAgents);

        group.MapPut("/{id:guid}", async (Guid id, UpdateAgentRequest request, IAgentService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.UpdateAsync(id, request, cancellationToken)))
            .RequireAuthorization(NexaPolicies.CanManageAgents);

        group.MapPost("/{id:guid}/versions", async (Guid id, PublishAgentVersionRequest request, IAgentService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.PublishVersionAsync(id, request, cancellationToken)))
            .RequireAuthorization(NexaPolicies.CanManageAgents);

        group.MapPost("/{id:guid}/archive", async (Guid id, IAgentService service, CancellationToken cancellationToken) =>
            {
                await service.ArchiveAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(NexaPolicies.CanManageAgents);

        return api;
    }
}
