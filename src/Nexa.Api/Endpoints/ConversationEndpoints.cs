using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexa.Application.Common;
using Nexa.Application.Conversations;
using Nexa.Contracts.Conversations;

namespace Nexa.Api.Endpoints;

public static class ConversationEndpoints
{
    public static RouteGroupBuilder MapConversationEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/conversations").RequireAuthorization(NexaPolicies.CanView);

        group.MapGet("/", async (IConversationService service, bool includeArchived, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(includeArchived, cancellationToken)));

        group.MapGet("/{id:guid}", async (Guid id, IConversationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetAsync(id, cancellationToken)));

        group.MapPost("/", async (CreateConversationRequest request, IConversationService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateAsync(request, cancellationToken);
                return Results.Created($"/api/conversations/{created.Id}", created);
            })
            .RequireAuthorization(NexaPolicies.CanChat);

        group.MapPost("/{id:guid}/rename", async (Guid id, RenameConversationRequest request, IConversationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.RenameAsync(id, request, cancellationToken)))
            .RequireAuthorization(NexaPolicies.CanChat);

        group.MapPost("/{id:guid}/archive", async (Guid id, IConversationService service, CancellationToken cancellationToken) =>
            {
                await service.ArchiveAsync(id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization(NexaPolicies.CanChat);

        group.MapPost("/{id:guid}/messages", async (Guid id, SendMessageRequest request, IConversationService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.SendMessageAsync(id, request, cancellationToken)))
            .RequireAuthorization(NexaPolicies.CanChat);

        return api;
    }
}
