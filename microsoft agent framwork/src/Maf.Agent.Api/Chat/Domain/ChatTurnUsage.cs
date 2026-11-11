namespace Maf.Agent.Api.Chat.Domain;

public sealed class ChatTurnUsage
{
    private ChatTurnUsage()
    {
    }

    private ChatTurnUsage(
        long? inputTokens,
        long? outputTokens,
        long? reasoningTokens,
        long? cachedInputTokens,
        long? totalTokens,
        long? cacheWriteInputTokens)
    {
        InputTokens = RequireCount(inputTokens);
        OutputTokens = RequireCount(outputTokens);
        ReasoningTokens = RequireCount(reasoningTokens);
        CachedInputTokens = RequireCount(cachedInputTokens);
        TotalTokens = RequireCount(totalTokens);
        CacheWriteInputTokens = RequireCount(cacheWriteInputTokens);
    }

    public long EntryId { get; private set; }

    public long? InputTokens { get; private set; }

    public long? OutputTokens { get; private set; }

    public long? ReasoningTokens { get; private set; }

    public long? CachedInputTokens { get; private set; }

    public long? TotalTokens { get; private set; }

    public long? CacheWriteInputTokens { get; private set; }

    public static ChatTurnUsage? Record(
        long? inputTokens,
        long? outputTokens,
        long? reasoningTokens,
        long? cachedInputTokens,
        long? totalTokens,
        long? cacheWriteInputTokens = null)
    {
        if (inputTokens is null &&
            outputTokens is null &&
            reasoningTokens is null &&
            cachedInputTokens is null &&
            totalTokens is null &&
            cacheWriteInputTokens is null)
        {
            return null;
        }

        return new ChatTurnUsage(
            inputTokens,
            outputTokens,
            reasoningTokens,
            cachedInputTokens,
            totalTokens,
            cacheWriteInputTokens);
    }

    private static long? RequireCount(long? count)
    {
        if (count is < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "تعداد توکن منفی نیست.");
        }

        return count;
    }
}
