using Microsoft.SemanticKernel.ChatCompletion;

namespace ollama.Middleware;

public sealed class ChatMiddlewareContext
{
    public required string UserMessage { get; init; }

    public IEnumerable<string>? ToolNames { get; init; }

    public ChatHistory? History { get; init; }

    public Func<string>? BuildSystemPrompt { get; init; }
}

public delegate Task<string> ChatHandlerDelegate();

public interface IChatMiddleware
{
    Task<string> InvokeAsync(ChatMiddlewareContext context, ChatHandlerDelegate next);
}

public sealed class ChatMiddlewarePipeline(IEnumerable<IChatMiddleware> middlewares)
{
    public Task<string> ExecuteAsync(ChatMiddlewareContext context, ChatHandlerDelegate handler)
    {
        ChatHandlerDelegate pipeline = handler;

        foreach (var middleware in middlewares.Reverse())
        {
            var next = pipeline;
            var current = middleware;
            pipeline = () => current.InvokeAsync(context, next);
        }

        return pipeline();
    }
}
