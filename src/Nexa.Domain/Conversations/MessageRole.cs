namespace Nexa.Domain.Conversations;

    public enum MessageRole
{
    User = 0,
    Assistant = 1,
    System = 2,
    Error = 3,
    ToolCall = 4,
    ToolResult = 5
}
