using Microsoft.SemanticKernel;

namespace ollama.Middleware;

public sealed class PromptSafetyFilter : IPromptRenderFilter
{
    private static readonly string[] Blocked = ["DROP TABLE", "DELETE FROM", "--"];

    public async Task OnPromptRenderAsync(
        PromptRenderContext context,
        Func<PromptRenderContext, Task> next)
    {
        await next(context);

        if (Blocked.Any(b => context.RenderedPrompt?.Contains(b, StringComparison.OrdinalIgnoreCase) == true))
        {
            throw new InvalidOperationException("Unsafe prompt detected.");
        }
    }
}
