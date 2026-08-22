using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Nexa.Application.Common;
using Nexa.Application.Tools;

namespace Nexa.Api.Endpoints;

public static class ToolEndpoints
{
    public static RouteGroupBuilder MapToolEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/tools").RequireAuthorization(NexaPolicies.CanView);
        group.MapGet("/", (IToolCatalogService tools) => Results.Ok(tools.List()));
        return api;
    }
}
