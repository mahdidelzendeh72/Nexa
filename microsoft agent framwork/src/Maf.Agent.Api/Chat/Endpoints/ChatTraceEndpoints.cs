using System.Security.Claims;
using Maf.Agent.Api.Chat.Domain;
using Maf.Agent.Api.Chat.Infrastructure.History;

namespace Maf.Agent.Api.Chat.Endpoints;

public static class ChatTraceEndpoints
{
    public static IEndpointRouteBuilder MapChatTraceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/chat/traces", ListAsync)
            .WithTags("Chat")
            .RequireAuthorization();
        endpoints.MapGet("/api/chat/traces/{threadId}", GetAsync)
            .WithTags("Chat")
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        ClaimsPrincipal user,
        ChatTraceReader reader,
        CancellationToken cancellationToken)
    {
        if (user.FindFirstValue(ClaimTypes.NameIdentifier) is not string userId)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await reader.ListThreadsAsync(userId, cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        string threadId,
        ClaimsPrincipal user,
        ChatTraceReader reader,
        CancellationToken cancellationToken)
    {
        if (user.FindFirstValue(ClaimTypes.NameIdentifier) is not string userId)
        {
            return Results.Unauthorized();
        }

        if (!ChatConversation.IsThreadId(threadId))
        {
            return Results.NotFound();
        }

        return Results.Ok(await reader.GetThreadAsync(userId, threadId, cancellationToken));
    }
}
