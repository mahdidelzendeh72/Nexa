using System.Security.Claims;
using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Domain;

namespace Maf.Agent.Api.Chat.Endpoints;

public static class ChatHistoryEndpoints
{
    public static IEndpointRouteBuilder MapChatHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/chat/conversations")
            .WithTags("Chat")
            .RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapGet("/{threadId}", GetAsync);
        group.MapPut("/{threadId}/entries/{sequence:int}/reaction", ReactAsync);
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        string surface,
        ClaimsPrincipal user,
        IChatHistoryStore store,
        CancellationToken cancellationToken)
    {
        if (Owner(user) is not string ownerUserId)
        {
            return Results.Unauthorized();
        }

        if (!ChatConversation.IsSurface(surface))
        {
            return Results.BadRequest(new { error = "سطح گفتگو شناخته نیست." });
        }

        ChatConversation[] conversations = (await store.ListAsync(ownerUserId, surface, cancellationToken)).ToArray();
        return Results.Ok(conversations.Select(ToSummary).ToArray());
    }

    private static async Task<IResult> GetAsync(
        string threadId,
        ClaimsPrincipal user,
        IChatHistoryStore store,
        CancellationToken cancellationToken)
    {
        if (Owner(user) is not string ownerUserId)
        {
            return Results.Unauthorized();
        }

        ChatConversation? conversation = await store.FindAsync(threadId, ownerUserId, cancellationToken);
        if (conversation is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(new ChatTranscriptResponse(
            conversation.ThreadId,
            conversation.Surface,
            conversation.Title,
            conversation.ProviderId,
            conversation.ModelName,
            conversation.Entries
                .OrderBy(entry => entry.Sequence)
                .Select(entry => new ChatTranscriptMessageResponse(
                    entry.Sequence,
                    entry.Role.ToString(),
                    entry.Text,
                    entry.Reaction.ToString(),
                    entry.Attachments
                        .OrderBy(attachment => attachment.Sequence)
                        .Select(attachment => new ChatTranscriptAttachmentResponse(
                            attachment.FileName,
                            attachment.MediaType,
                            attachment.ContentBase64))
                        .ToArray(),
                    entry.Usage is null
                        ? null
                        : new ChatTranscriptUsageResponse(
                            entry.Usage.InputTokens,
                            entry.Usage.OutputTokens,
                            entry.Usage.ReasoningTokens,
                            entry.Usage.CachedInputTokens,
                            entry.Usage.TotalTokens,
                            entry.Usage.CacheWriteInputTokens)))
                .ToArray()));
    }

    private static async Task<IResult> ReactAsync(
        string threadId,
        int sequence,
        SetChatReactionRequest request,
        ClaimsPrincipal user,
        IChatHistoryStore store,
        CancellationToken cancellationToken)
    {
        if (Owner(user) is not string ownerUserId)
        {
            return Results.Unauthorized();
        }

        if (!Enum.TryParse(request.Reaction, ignoreCase: false, out ChatReaction reaction) ||
            !Enum.IsDefined(reaction))
        {
            return Results.BadRequest(new { error = "بازخورد باید None، Like یا Dislike باشد." });
        }

        try
        {
            bool updated = await store.ReactAsync(threadId, ownerUserId, sequence, reaction, cancellationToken);
            return updated ? Results.NoContent() : Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    private static string? Owner(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);

    private static ChatSummaryResponse ToSummary(ChatConversation conversation) =>
        new(
            conversation.ThreadId,
            conversation.Title,
            conversation.ProviderId,
            conversation.ModelName,
            conversation.UpdatedAt);

    private sealed record SetChatReactionRequest(string? Reaction);

    private sealed record ChatSummaryResponse(
        string ThreadId,
        string Title,
        Guid? ProviderId,
        string? ModelName,
        DateTimeOffset UpdatedAt);

    private sealed record ChatTranscriptAttachmentResponse(string FileName, string MediaType, string ContentBase64);

    private sealed record ChatTranscriptUsageResponse(
        long? InputTokens,
        long? OutputTokens,
        long? ReasoningTokens,
        long? CachedInputTokens,
        long? TotalTokens,
        long? CacheWriteInputTokens);

    private sealed record ChatTranscriptMessageResponse(
        int Sequence,
        string Role,
        string Text,
        string Reaction,
        ChatTranscriptAttachmentResponse[] Attachments,
        ChatTranscriptUsageResponse? Usage);

    private sealed record ChatTranscriptResponse(
        string ThreadId,
        string Surface,
        string Title,
        Guid? ProviderId,
        string? ModelName,
        ChatTranscriptMessageResponse[] Messages);
}
