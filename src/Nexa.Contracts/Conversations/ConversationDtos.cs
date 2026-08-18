namespace Nexa.Contracts.Conversations;

public sealed record ConversationDto(
    Guid Id,
    Guid AgentVersionId,
    Guid AgentId,
    string AgentName,
    int AgentVersionNumber,
    string Title,
    bool IsArchived,
    bool IsPinned,
    bool IsFavorite,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ConversationMessageDto(
    Guid Id,
    string Role,
    string Content,
    int? InputTokens,
    int? OutputTokens,
    int? TotalTokens,
    int? LatencyMs,
    DateTimeOffset CreatedAt);

public sealed record ConversationDetailDto(
    ConversationDto Conversation,
    IReadOnlyList<ConversationMessageDto> Messages);

public sealed record CreateConversationRequest(Guid AgentId, string? Title);

public sealed record RenameConversationRequest(string Title);

public sealed record SendMessageRequest(string Content);

public sealed record SendMessageResponse(
    ConversationMessageDto UserMessage,
    ConversationMessageDto AssistantMessage);
