using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Chat.Infrastructure.Ledgers;

public static class ChatRequestUsage
{
    public const string ItemKey = "maf.chat.usage";

    public static void Observe(HttpContext? http, UsageDetails? details)
    {
        if (http is null || details is null)
        {
            return;
        }

        if (http.Items[ItemKey] is not List<UsageDetails> observed)
        {
            observed = [];
            http.Items[ItemKey] = observed;
        }

        observed.Add(Copy(details));
    }

    private static UsageDetails Copy(UsageDetails details)
    {
        UsageDetails copy = new()
        {
            InputTokenCount = details.InputTokenCount,
            OutputTokenCount = details.OutputTokenCount,
            TotalTokenCount = details.TotalTokenCount,
            CachedInputTokenCount = details.CachedInputTokenCount,
            ReasoningTokenCount = details.ReasoningTokenCount,
        };
        if (details.AdditionalCounts is { } counts &&
            counts.TryGetValue(ChatUsageLedger.CacheWriteInputTokensKey, out long cacheWrite))
        {
            copy.AdditionalCounts = new() { [ChatUsageLedger.CacheWriteInputTokensKey] = cacheWrite };
        }

        return copy;
    }

    public static IReadOnlyList<UsageDetails> Drain(HttpContext? http)
    {
        if (http?.Items[ItemKey] is not List<UsageDetails> observed)
        {
            return [];
        }

        http.Items.Remove(ItemKey);
        return observed;
    }
}
