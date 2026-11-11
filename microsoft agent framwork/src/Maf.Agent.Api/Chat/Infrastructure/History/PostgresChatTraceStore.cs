using Maf.Agent.Api.Chat.Application;
using Maf.Agent.Api.Chat.Domain;
using Maf.Agent.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Chat.Infrastructure.History;

public sealed class PostgresChatTraceStore(ApplicationDbContext dbContext) : IChatTraceStore
{
    public async Task SaveAsync(IReadOnlyList<ChatTraceRunDraft> runs, CancellationToken cancellationToken)
    {
        if (runs.Count == 0)
        {
            return;
        }

        foreach (IGrouping<string, ChatTraceRunDraft> thread in runs.GroupBy(run => run.ThreadId, StringComparer.Ordinal))
        {
            string userId = thread.Select(run => run.UserId).LastOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? "";
            ChatTraceThread? stored = await dbContext.ChatTraceThreads
                .SingleOrDefaultAsync(item => item.ThreadId == thread.Key, cancellationToken);
            if (stored is null)
            {
                stored = ChatTraceThread.Open(thread.Key, Trim(userId, 450), DateTimeOffset.UtcNow);
                dbContext.ChatTraceThreads.Add(stored);
            }
            else
            {
                stored.RememberUser(Trim(userId, 450));
            }

            string[] runIds = thread.Select(run => run.RunId).ToArray();
            List<ChatTraceRun> existing = await dbContext.ChatTraceRuns
                .Include(run => run.Steps)
                .Where(run => runIds.Contains(run.RunId))
                .ToListAsync(cancellationToken);
            foreach (ChatTraceRunDraft draft in thread)
            {
                ChatTraceRun? run = existing.FirstOrDefault(item => item.RunId == draft.RunId);
                bool isNew = run is null;
                if (run is null)
                {
                    run = ChatTraceRun.Start(Trim(draft.RunId, 80), thread.Key, draft.StartedAt);
                    dbContext.ChatTraceRuns.Add(run);
                }

                bool newlyFinished = run.Complete(
                    draft.FinishedAt,
                    TrimOrNull(draft.Outcome, 32),
                    TrimOrNull(draft.Model, 200),
                    draft.InputTokens,
                    draft.OutputTokens,
                    draft.ReasoningTokens,
                    draft.TotalTokens);
                stored.NoteRun(isNew, draft.FinishedAt, TrimOrNull(draft.Outcome, 32), newlyFinished ? draft.TotalTokens ?? 0 : 0);
                foreach (ChatTraceStepDraft step in draft.Steps)
                {
                    run.AppendStep(
                        step.Sequence,
                        Trim(step.Kind, 32),
                        TrimOrNull(step.RefId, 80),
                        TrimOrNull(step.Name, 200),
                        step.Size);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ChatTraceThreadView>> ListThreadsAsync(
        string userId,
        int take,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.ChatTraceThreads
            .AsNoTracking()
            .Where(thread => thread.UserId == userId)
            .OrderByDescending(thread => thread.LastRunAt)
            .Take(take)
            .Select(thread => new
            {
                thread.ThreadId,
                thread.LastRunAt,
                thread.LastOutcome,
                thread.RunCount,
                Model = dbContext.ChatTraceRuns
                    .Where(run => run.ThreadId == thread.ThreadId)
                    .OrderByDescending(run => run.StartedAt)
                    .Select(run => run.Model)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ChatTraceThreadView(
                row.ThreadId,
                row.LastRunAt,
                row.LastOutcome,
                row.Model,
                row.RunCount,
                false))
            .ToArray();
    }

    public async Task<IReadOnlyList<ChatTraceRunView>> ListByThreadAsync(
        string userId,
        string threadId,
        CancellationToken cancellationToken)
    {
        List<ChatTraceRun> runs = await dbContext.ChatTraceRuns
            .AsNoTracking()
            .Include(run => run.Steps)
            .Where(run => run.ThreadId == threadId)
            .Where(run => dbContext.ChatTraceThreads.Any(thread =>
                thread.ThreadId == run.ThreadId && thread.UserId == userId))
            .OrderBy(run => run.StartedAt)
            .ToListAsync(cancellationToken);

        return runs.Select(ToView).ToArray();
    }

    private static ChatTraceRunView ToView(ChatTraceRun run) =>
        new(
            run.ThreadId,
            run.RunId,
            run.StartedAt,
            run.FinishedAt,
            run.Outcome,
            run.Model,
            run.InputTokens,
            run.OutputTokens,
            run.ReasoningTokens,
            run.TotalTokens,
            run.Steps
                .OrderBy(step => step.Sequence)
                .Select(step => new ChatTraceStepView(step.Sequence, step.Kind, step.RefId, step.Name, step.Size))
                .ToArray());

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max];

    private static string? TrimOrNull(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : Trim(value, max);
}
