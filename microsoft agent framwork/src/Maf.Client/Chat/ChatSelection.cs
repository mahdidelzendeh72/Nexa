namespace Maf.Client.Chat;

public sealed class ChatSelection
{
    public Guid ProviderId { get; set; }

    public string ModelName { get; set; } = "";

    public IReadOnlyList<string> ToolKeys { get; set; } = [];
}
