using Maf.Agent.Api.Data;
using Maf.Agent.Api.Knowledge.Application;
using Maf.Agent.Api.Knowledge.Domain;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Knowledge.Infrastructure;

public sealed class KnowledgeIngestionWorker(
    IKnowledgeIngestionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<KnowledgeIngestionWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await EnqueueIncompleteDocumentsAsync(stoppingToken);

        await foreach (Guid documentId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(documentId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Unhandled knowledge ingestion error for document {DocumentId}.",
                    documentId);
            }
        }
    }

    private async Task EnqueueIncompleteDocumentsAsync(
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Guid[] documentIds = await dbContext.KnowledgeDocuments
            .Where(document =>
                document.Status == KnowledgeDocumentStatus.Pending ||
                document.Status == KnowledgeDocumentStatus.Processing)
            .Select(document => document.Id)
            .ToArrayAsync(cancellationToken);

        foreach (Guid documentId in documentIds)
        {
            await queue.EnqueueAsync(documentId, cancellationToken);
        }
    }

    private async Task ProcessAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ApplicationDbContext dbContext =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IKnowledgeDocumentStorage storage =
            scope.ServiceProvider.GetRequiredService<IKnowledgeDocumentStorage>();
        IKnowledgeChunker chunker =
            scope.ServiceProvider.GetRequiredService<IKnowledgeChunker>();
        IKnowledgeEmbeddingService embeddingService =
            scope.ServiceProvider.GetRequiredService<IKnowledgeEmbeddingService>();
        IEnumerable<IDocumentTextExtractor> extractors =
            scope.ServiceProvider.GetServices<IDocumentTextExtractor>();

        KnowledgeDocument? document =
            await dbContext.KnowledgeDocuments.SingleOrDefaultAsync(
                item => item.Id == documentId,
                cancellationToken);
        if (document is null ||
            document.Status is KnowledgeDocumentStatus.Ready or
                KnowledgeDocumentStatus.ReadyWithoutEmbedding)
        {
            return;
        }

        int attemptNumber = await dbContext.KnowledgeProcessingAttempts
            .Where(attempt => attempt.DocumentId == documentId)
            .CountAsync(cancellationToken) + 1;
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        KnowledgeProcessingAttempt attempt =
            KnowledgeProcessingAttempt.Start(documentId, attemptNumber, startedAt);
        dbContext.KnowledgeProcessingAttempts.Add(attempt);
        document.StartProcessing(startedAt);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await using KnowledgeDocumentStream stored =
                await storage.OpenReadAsync(documentId, cancellationToken)
                ?? throw new InvalidOperationException("The original document is missing.");
            await using FileStream seekable =
                await CopyToSeekableTempFileAsync(stored.Content, cancellationToken);

            IDocumentTextExtractor extractor = extractors.FirstOrDefault(
                candidate => candidate.CanExtract(stored.FileName, stored.ContentType))
                ?? throw new NotSupportedException(
                    $"The file type '{Path.GetExtension(stored.FileName)}' is not supported.");

            IReadOnlyList<ExtractedSection> sections =
                await extractor.ExtractAsync(seekable, cancellationToken);
            attempt.BeginChunking();
            await dbContext.SaveChangesAsync(cancellationToken);

            IReadOnlyList<KnowledgeChunkDraft> drafts = chunker.CreateChunks(sections);
            if (drafts.Count == 0)
            {
                throw new InvalidOperationException(
                    "No searchable text could be extracted from the document.");
            }

            attempt.BeginEmbedding();
            await dbContext.SaveChangesAsync(cancellationToken);
            KnowledgeEmbeddingBatch embeddingBatch =
                await GenerateEmbeddingsAsync(
                    embeddingService,
                    drafts,
                    cancellationToken);

            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.KnowledgeEmbeddings
                .Where(embedding => embedding.Chunk.DocumentId == documentId)
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.KnowledgeChunks
                .Where(chunk => chunk.DocumentId == documentId)
                .ExecuteDeleteAsync(cancellationToken);

            List<KnowledgeChunk> chunks = drafts
                .Select(draft => KnowledgeChunk.Create(
                    documentId,
                    draft.Ordinal,
                    draft.Content,
                    draft.NormalizedContent,
                    draft.TokenEstimate,
                    draft.PageNumber,
                    draft.SheetName,
                    draft.Section))
                .ToList();
            dbContext.KnowledgeChunks.AddRange(chunks);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (embeddingBatch.Available)
            {
                DateTimeOffset embeddingsCreatedAt = DateTimeOffset.UtcNow;
                for (int index = 0; index < chunks.Count; index++)
                {
                    ReadOnlyMemory<float> vector = embeddingBatch.Vectors[index];
                    dbContext.KnowledgeEmbeddings.Add(KnowledgeEmbedding.Create(
                        chunks[index].Id,
                        embeddingBatch.Model!,
                        vector,
                        embeddingsCreatedAt));
                }
            }

            DateTimeOffset completedAt = DateTimeOffset.UtcNow;
            document.CompleteProcessing(
                chunks.Count,
                embeddingBatch.Available
                ? embeddingBatch.Model
                : null,
                completedAt);
            attempt.Complete(completedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Knowledge document {DocumentId} processed into {ChunkCount} chunks with status {Status}.",
                documentId,
                chunks.Count,
                document.Status);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            dbContext.ChangeTracker.Clear();
            KnowledgeDocument failedDocument =
                await dbContext.KnowledgeDocuments.SingleAsync(
                    item => item.Id == documentId,
                    cancellationToken);
            KnowledgeProcessingAttempt failedAttempt =
                await dbContext.KnowledgeProcessingAttempts.SingleAsync(
                    item => item.Id == attempt.Id,
                    cancellationToken);
            DateTimeOffset failedAt = DateTimeOffset.UtcNow;
            failedDocument.FailProcessing(exception.Message, failedAt);
            failedAttempt.Fail(exception, failedAt);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                exception,
                "Knowledge ingestion failed for document {DocumentId}.",
                documentId);
        }
    }

    private static async Task<KnowledgeEmbeddingBatch> GenerateEmbeddingsAsync(
        IKnowledgeEmbeddingService embeddingService,
        IReadOnlyList<KnowledgeChunkDraft> chunks,
        CancellationToken cancellationToken)
    {
        List<ReadOnlyMemory<float>> vectors = [];
        string? model = null;

        foreach (KnowledgeChunkDraft[] batch in chunks.Chunk(32))
        {
            KnowledgeEmbeddingBatch result = await embeddingService.GenerateAsync(
                batch.Select(chunk => chunk.Content).ToArray(),
                cancellationToken);
            if (!result.Available)
            {
                return result;
            }

            model ??= result.Model;
            vectors.AddRange(result.Vectors);
        }

        return new KnowledgeEmbeddingBatch(true, model, vectors);
    }

    private static async Task<FileStream> CopyToSeekableTempFileAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        string tempPath = Path.GetTempFileName();
        FileStream target = new(
            tempPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        try
        {
            await source.CopyToAsync(target, cancellationToken);
            target.Position = 0;
            return target;
        }
        catch
        {
            await target.DisposeAsync();
            throw;
        }
    }
}
