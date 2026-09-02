using Nexa.Contracts.Conversations;
using Nexa.Domain.Agents;
using Nexa.Domain.Conversations;

namespace Nexa.Application.Conversations;

internal static class ConversationMapper
{
    public static ConversationDto ToDto(Conversation conversation, Agent agent, AgentVersion version) =>
        new(
            conversation.Id,
            conversation.AgentVersionId,
            agent.Id,
            agent.Name,
            version.VersionNumber,
            conversation.Title,
            conversation.IsArchived,
            conversation.IsPinned,
            conversation.IsFavorite,
            conversation.CreatedAt,
            conversation.UpdatedAt);

    public static ConversationMessageDto ToDto(ConversationMessage message) =>
        new(
            message.Id,
            message.Role.ToString(),
            message.Content,
            message.InputTokens,
            message.OutputTokens,
            message.TotalTokens,
            message.LatencyMs,
            message.CreatedAt);
}
