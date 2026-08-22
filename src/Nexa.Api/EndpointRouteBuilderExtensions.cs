using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nexa.Api.Endpoints;

namespace Nexa.Api;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapNexaApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapAgentEndpoints();
        api.MapModelEndpoints();
        api.MapConversationEndpoints();
        api.MapToolEndpoints();
        return app;
    }
}
