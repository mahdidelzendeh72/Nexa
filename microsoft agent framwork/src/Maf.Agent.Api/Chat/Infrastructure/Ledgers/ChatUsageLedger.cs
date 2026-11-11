using System.Collections.Concurrent;
using Maf.Agent.Api.Chat.Domain;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Chat.Infrastructure.Ledgers;

public sealed class ChatUsageLedger
{
    public const string CacheWriteInputTokensKey = "CacheWriteInputTokens";

    private readonly ConcurrentDictionary<string, Counts> _pending = new(StringComparer.Ordinal);

    public void Add(string threadId, UsageDetails? details)
    {
        if (string.IsNullOrWhiteSpace(threadId) || details is null)
        {
            return;
        }

        _pending.GetOrAdd(threadId, _ => new Counts()).Add(details);
    }

    public void Add(string threadId, ChatTurnUsage? usage)
    {
        if (string.IsNullOrWhiteSpace(threadId) || usage is null)
        {
            return;
        }

        _pending.GetOrAdd(threadId, _ => new Counts()).Add(usage);
    }

    public ChatTurnUsage? Take(string threadId) =>
        _pending.TryRemove(threadId, out Counts? counts) ? counts.ToUsage() : null;

    private sealed class Counts
    {
        private readonly Lock _gate = new();
        private long? _input;
        private long? _output;
        private long? _reasoning;
        private long? _cached;
        private long? _total;
        private long? _cacheWrite;

        public void Add(UsageDetails details)
        {
            lock (_gate)
            {
                AddCore(
                    details.InputTokenCount,
                    details.OutputTokenCount,
                    details.ReasoningTokenCount,
                    details.CachedInputTokenCount,
                    details.TotalTokenCount,
                    CacheWrite(details));
            }
        }

        public void Add(ChatTurnUsage usage)
        {
            lock (_gate)
            {
                AddCore(
                    usage.InputTokens,
                    usage.OutputTokens,
                    usage.ReasoningTokens,
                    usage.CachedInputTokens,
                    usage.TotalTokens,
                    usage.CacheWriteInputTokens);
            }
        }

        private void AddCore(
            long? input,
            long? output,
            long? reasoning,
            long? cached,
            long? total,
            long? cacheWrite)
        {
            _input = Sum(_input, input);
            _output = Sum(_output, output);
            _reasoning = Sum(_reasoning, reasoning);
            _cached = Sum(_cached, cached);
            _total = Sum(_total, total);
            _cacheWrite = Sum(_cacheWrite, cacheWrite);
        }

        public ChatTurnUsage? ToUsage()
        {
            lock (_gate)
            {
                return ChatTurnUsage.Record(_input, _output, _reasoning, _cached, _total, _cacheWrite);
            }
        }

        private static long? CacheWrite(UsageDetails details) =>
            details.AdditionalCounts is { } counts &&
            counts.TryGetValue(CacheWriteInputTokensKey, out long count)
                ? count
                : null;

        private static long? Sum(long? accumulated, long? reported) =>
            reported is null or < 0 ? accumulated : (accumulated ?? 0) + reported.Value;
    }
}
