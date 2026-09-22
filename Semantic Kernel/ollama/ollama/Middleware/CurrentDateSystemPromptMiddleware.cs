using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ollama.Middleware;

public sealed class CurrentDateSystemPromptMiddleware : IChatMiddleware
{
    public Task<string> InvokeAsync(ChatMiddlewareContext context, ChatHandlerDelegate next)
    {
        if (context.History is not null && context.BuildSystemPrompt is not null)
        {
            RefreshSystemPromptWithCurrentDate(context.History, context.BuildSystemPrompt);
        }

        return next();
    }

    private static void RefreshSystemPromptWithCurrentDate(ChatHistory history, Func<string> buildSystemPrompt)
    {
        if (history.Count == 0 || history[0].Role != AuthorRole.System)
        {
            history.Insert(0, new ChatMessageContent(AuthorRole.System, buildSystemPrompt()));
            return;
        }

        history[0].Content = buildSystemPrompt();
    }
}
