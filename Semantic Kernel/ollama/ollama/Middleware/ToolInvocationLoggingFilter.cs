using Microsoft.SemanticKernel;

namespace ollama.Middleware;

public sealed class ToolInvocationLoggingFilter : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        Console.WriteLine(
            $"[Middleware] Invoking tool: {context.Function.PluginName}-{context.Function.Name}");

        await next(context);

        Console.WriteLine(
            $"[Middleware] Tool result: {context.Result}");
    }
}
