namespace ollama.Middleware;

public sealed class LoggingChatMiddleware : IChatMiddleware
{
    public async Task<string> InvokeAsync(ChatMiddlewareContext context, ChatHandlerDelegate next)
    {
        var toolInfo = context.ToolNames is null
            ? "Auto"
            : string.Join(", ", context.ToolNames);

        Console.WriteLine($"[Middleware] Request: {context.UserMessage}");
        Console.WriteLine($"[Middleware] Tools: {toolInfo}");

        var response = await next();

        Console.WriteLine($"[Middleware] Response length: {response.Length} chars");

        return response;
    }
}
