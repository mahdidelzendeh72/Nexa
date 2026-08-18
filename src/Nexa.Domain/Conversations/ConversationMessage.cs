using Nexa.Domain.Common;

namespace Nexa.Domain.Conversations;

public sealed class ConversationMessage
{
    private ConversationMessage()
    {
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public MessageRole Role { get; private set; }
    public string Content { get; private set; } = null!;
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public int? TotalTokens { get; private set; }
    public int? LatencyMs { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static ConversationMessage Create(
        Guid conversationId,
        MessageRole role,
        string content,
        TokenUsage? usage,
        int? latencyMs,
        DateTimeOffset createdAt)
    {
        return new ConversationMessage
        {
            Id = Guid.CreateVersion7(),
            ConversationId = Guard.NotEmpty(conversationId, nameof(conversationId)),
            Role = role,
            Content = Guard.NotNullOrWhiteSpace(content, nameof(content), 100_000),
            InputTokens = usage?.InputTokens,
            OutputTokens = usage?.OutputTokens,
            TotalTokens = usage?.TotalTokens,
            LatencyMs = latencyMs,
            CreatedAt = createdAt
        };
    }
}
