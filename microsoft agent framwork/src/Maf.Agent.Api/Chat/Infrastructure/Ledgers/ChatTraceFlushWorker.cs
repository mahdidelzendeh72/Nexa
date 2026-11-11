using Maf.Agent.Api.Chat.Application;

namespace Maf.Agent.Api.Chat.Infrastructure.Ledgers;

public sealed class ChatTraceFlushWorker(
    ChatTraceLedger ledger,
    IServiceScopeFactory scopes,
    ILogger<ChatTraceFlushWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _flush = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FlushAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await FlushAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "رد گفتگو هنگام توقف ذخیره نشد.");
        }

        await base.StopAsync(cancellationToken);
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (!await _flush.WaitAsync(0, cancellationToken))
        {
            return;
        }

        try
        {
            IReadOnlyList<ChatTraceRunDraft> drafts = ledger.Detach();
            if (drafts.Count == 0)
            {
                return;
            }

            try
            {
                await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                IChatTraceStore store = scope.ServiceProvider.GetRequiredService<IChatTraceStore>();
                await store.SaveAsync(drafts, cancellationToken);
                ledger.Commit(drafts);
            }
            catch (Exception exception)
            {
                ledger.Abandon(drafts);
                logger.LogWarning(exception, "رد گفتگو در دیتابیس ذخیره نشد.");
            }
        }
        finally
        {
            _flush.Release();
        }
    }
}
