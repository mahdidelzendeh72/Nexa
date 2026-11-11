using Maf.Agent.Api.Chat.Application;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Chat.Infrastructure.ContextProviders;

public sealed class ChatToolContextProvider(
    IHttpContextAccessor http,
    ChatToolCatalog catalog,
    ILogger<ChatToolContextProvider> logger) : AIContextProvider
{
    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        HttpContext? httpContext = http.HttpContext;
        if (httpContext is null)
        {
            return new AIContext();
        }

        IReadOnlyList<ChatToolKey> keys = ChatToolSelection.Parse(httpContext.Request.Headers[ChatToolSelection.Header]);
        if (keys.Count == 0)
        {
            return new AIContext();
        }

        try
        {
            IReadOnlyList<AITool> tools = await catalog.ResolveAsync(keys, cancellationToken);
            return tools.Count == 0 ? new AIContext() : new AIContext { Tools = tools };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "ابزارهای انتخاب‌شده برای این گفتگو ثبت نشد.");
            return new AIContext();
        }
    }
}
