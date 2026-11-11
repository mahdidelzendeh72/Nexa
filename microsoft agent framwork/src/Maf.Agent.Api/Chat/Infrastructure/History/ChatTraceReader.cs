using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Infrastructure.Ledgers;

namespace Maf.Agent.Api.Chat.Infrastructure.History;

public sealed class ChatTraceReader(IChatTraceStore store, ChatTraceLedger ledger)
{
    public const int Take = 40;

    public async Task<IReadOnlyList<ChatTraceThreadView>> ListThreadsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChatTraceThreadView> stored = await store.ListThreadsAsync(userId, Take, cancellationToken);
        return ChatTraceTimeline.Threads(stored, ledger.Snapshot(userId), Take);
    }

    public async Task<IReadOnlyList<ChatTraceRunView>> GetThreadAsync(
        string userId,
        string threadId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ChatTraceRunView> stored = await store.ListByThreadAsync(userId, threadId, cancellationToken);
        ChatTraceRunDraft[] live = ledger.Snapshot(userId)
            .Where(run => string.Equals(run.ThreadId, threadId, StringComparison.Ordinal))
            .ToArray();
        return ChatTraceTimeline.Merge(stored, live, int.MaxValue)
            .Select(ChatTraceTimeline.FoldReasoning)
            .OrderBy(run => run.StartedAt)
            .ToArray();
    }
}
