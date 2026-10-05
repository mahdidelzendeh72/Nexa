using Microsoft.SemanticKernel.ChatCompletion;

namespace ollama.Sessions;

public sealed class ChatSession
{
    public required string SessionId { get; init; }

    public required string UserId { get; init; }

    public ChatHistory History { get; } = new();

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public string? Title { get; set; }

    public string[]? SelectedToolNames { get; set; }

    public string? ModelId { get; set; }

    public float? Temperature { get; set; }

    public Dictionary<string, SessionAttachment> Attachments { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record ChatSessionSummary(
    string SessionId,
    string? Title,
    DateTime CreatedAt,
    int MessageCount,
    bool IsActive);
