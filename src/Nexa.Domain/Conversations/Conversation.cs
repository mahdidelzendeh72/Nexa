using Nexa.Domain.Common;

namespace Nexa.Domain.Conversations;

public sealed class Conversation
{
    public const string DefaultTitle = "New conversation";

    private readonly List<ConversationMessage> _messages = [];

    private Conversation()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid AgentVersionId { get; private set; }
    public string Title { get; private set; } = null!;
    public bool IsArchived { get; private set; }
    public bool IsPinned { get; private set; }
    public bool IsFavorite { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public uint RowVersion { get; private set; }

    public IReadOnlyCollection<ConversationMessage> Messages => _messages;

    public static Conversation Create(Guid userId, Guid agentVersionId, string? title = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Conversation
        {
            Id = Guid.CreateVersion7(),
            UserId = Guard.NotEmpty(userId, nameof(userId)),
            AgentVersionId = Guard.NotEmpty(agentVersionId, nameof(agentVersionId)),
            Title = string.IsNullOrWhiteSpace(title) ? DefaultTitle : Guard.NotNullOrWhiteSpace(title, nameof(title), 200),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public ConversationMessage AddMessage(
        MessageRole role,
        string content,
        TokenUsage? usage = null,
        int? latencyMs = null)
    {
        var message = ConversationMessage.Create(Id, role, content, usage, latencyMs, DateTimeOffset.UtcNow);
        _messages.Add(message);

        if (role == MessageRole.User && Title == DefaultTitle)
        {
            Title = TruncateTitle(content);
        }

        Touch();
        return message;
    }

    public void Rename(string title)
    {
        Title = Guard.NotNullOrWhiteSpace(title, nameof(title), 200);
        Touch();
    }

    public void Archive()
    {
        IsArchived = true;
        Touch();
    }

    public void SetPinned(bool isPinned)
    {
        IsPinned = isPinned;
        Touch();
    }

    public void SetFavorite(bool isFavorite)
    {
        IsFavorite = isFavorite;
        Touch();
    }

    public void EnsureOwnedBy(Guid userId)
    {
        if (UserId != userId)
        {
            throw new UnauthorizedAccessException("Conversation does not belong to the current user.");
        }
    }

    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
    }

    private static string TruncateTitle(string content)
    {
        var trimmed = content.Trim();
        return trimmed.Length <= 80 ? trimmed : trimmed[..80];
    }
}
