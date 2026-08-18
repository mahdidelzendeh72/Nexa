using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexa.Application.Common;
using Nexa.Application.Models;
using Nexa.Contracts.Models;

namespace Nexa.Api.Endpoints;

public static class ModelEndpoints
{
    public static RouteGroupBuilder MapModelEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/model-providers", async (IModelProfileService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.ListProvidersAsync(cancellationToken)))
            .RequireAuthorization(NexaPolicies.CanView);

        var profiles = api.MapGroup("/model-profiles").RequireAuthorization(NexaPolicies.CanView);

        profiles.MapGet("/", async (IModelProfileService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.ListProfilesAsync(cancellationToken)));

        profiles.MapGet("/{id:guid}", async (Guid id, IModelProfileService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetProfileAsync(id, cancellationToken)));

        profiles.MapPost("/", async (CreateModelProfileRequest request, IModelProfileService service, CancellationToken cancellationToken) =>
            {
                var created = await service.CreateProfileAsync(request, cancellationToken);
                return Results.Created($"/api/model-profiles/{created.Id}", created);
            })
            .RequireAuthorization(NexaPolicies.CanManageModels);

        profiles.MapPut("/{id:guid}", async (Guid id, UpdateModelProfileRequest request, IModelProfileService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.UpdateProfileAsync(id, request, cancellationToken)))
            .RequireAuthorization(NexaPolicies.CanManageModels);

        return api;
    }
}
