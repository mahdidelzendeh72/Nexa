using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Chat.Infrastructure.Middleware;

public sealed class ToolCallMiddleware(ILogger<ToolCallMiddleware> logger)
{
    public async ValueTask<object?> Invoke(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        string name = context.Function.Name;
        logger.LogInformation("فراخوانی ابزار {Tool} در {Agent} شروع شد.", name, agent.Name);
        try
        {
            object? result = await next(context, cancellationToken).ConfigureAwait(false);
            logger.LogInformation("فراخوانی ابزار {Tool} تمام شد.", name);
            return result;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "فراخوانی ابزار {Tool} ناموفق بود.", name);
            throw;
        }
    }
}
