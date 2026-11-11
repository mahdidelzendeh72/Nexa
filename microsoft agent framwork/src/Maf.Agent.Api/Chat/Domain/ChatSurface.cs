namespace Maf.Agent.Api.Chat.Domain;

public sealed record ChatSurface(string Name, string Path)
{
    public const string Instructions =
        "You are a helpful assistant. Reply in the user's language. Format every reply as Markdown.";

    public static ChatSurface General { get; } = new("general", "/ag-ui/general");

    public static ChatSurface Prompt { get; } = new("prompt", "/ag-ui/prompt");

    public static ChatSurface Workflow { get; } = new("workflow", "/ag-ui/workflow");

    public static IReadOnlyList<ChatSurface> All { get; } = [General, Prompt, Workflow];
}
