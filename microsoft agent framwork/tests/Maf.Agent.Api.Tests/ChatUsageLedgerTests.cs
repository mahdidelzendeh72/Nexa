using Maf.Agent.Api.Chat.Domain;
using Maf.Agent.Api.Chat.Infrastructure.Ledgers;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Tests;

public sealed class ChatUsageLedgerTests
{
    [Fact]
    public void Reports_from_several_model_calls_add_up_and_unreported_counts_stay_empty()
    {
        ChatUsageLedger ledger = new();
        ledger.Add("thread-1", new UsageDetails
        {
            InputTokenCount = 10,
            OutputTokenCount = 4,
            ReasoningTokenCount = 1,
            TotalTokenCount = 14,
            AdditionalCounts = new() { [ChatUsageLedger.CacheWriteInputTokensKey] = 6 },
        });
        ledger.Add("thread-1", new UsageDetails
        {
            InputTokenCount = 3,
            CachedInputTokenCount = 2,
            OutputTokenCount = 1,
            TotalTokenCount = 4,
        });

        ChatTurnUsage usage = ledger.Take("thread-1")!;

        Assert.Equal(13, usage.InputTokens);
        Assert.Equal(5, usage.OutputTokens);
        Assert.Equal(1, usage.ReasoningTokens);
        Assert.Equal(2, usage.CachedInputTokens);
        Assert.Equal(18, usage.TotalTokens);
        Assert.Equal(6, usage.CacheWriteInputTokens);
        Assert.Null(ledger.Take("thread-1"));
    }

    [Fact]
    public void A_failed_save_puts_the_counts_back_for_the_next_turn()
    {
        ChatUsageLedger ledger = new();
        ledger.Add("thread-1", new UsageDetails { InputTokenCount = 8, OutputTokenCount = 2, TotalTokenCount = 10 });
        ChatTurnUsage taken = ledger.Take("thread-1")!;

        ledger.Add("thread-1", new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1, TotalTokenCount = 2 });
        ledger.Add("thread-1", taken);

        ChatTurnUsage restored = ledger.Take("thread-1")!;
        Assert.Equal(9, restored.InputTokens);
        Assert.Equal(3, restored.OutputTokens);
        Assert.Equal(12, restored.TotalTokens);
    }
}
