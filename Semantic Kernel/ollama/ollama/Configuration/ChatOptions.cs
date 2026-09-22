namespace ollama.Configuration;

public sealed class ChatOptions
{
    public bool ToolCallingEnabled { get; init; }

    public string DefaultUserId { get; init; } = "default";

    public long MaxAttachmentBytes { get; init; } = 5 * 1024 * 1024;

    public int MaxAttachmentTextCharsInPrompt { get; init; } = 32_000;
}
