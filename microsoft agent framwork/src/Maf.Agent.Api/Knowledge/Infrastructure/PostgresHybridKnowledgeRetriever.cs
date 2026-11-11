using Maf.Agent.Api.Data;
using Maf.Agent.Api.Knowledge.Application;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Maf.Agent.Api.Knowledge.Infrastructure;

public sealed class PostgresHybridKnowledgeRetriever(
    ApplicationDbContext dbContext,
    ITextNormalizer normalizer,
    IKnowledgeEmbeddingService embeddingService)
    : IKnowledgeRetriever
{
    private const int RrfConstant = 60;

    public async Task<KnowledgeSearchResponse> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        limit = Math.Clamp(limit, 1, 50);
        int candidateLimit = Math.Max(20, limit * 4);
        string normalized = normalizer.Normalize(query);

        List<SearchCandidate> fullText = await SearchFullTextAsync(
            normalized,
            candidateLimit,
            cancellationToken);
        List<SearchCandidate> fuzzy = await SearchFuzzyAsync(
            normalized,
            candidateLimit,
            cancellationToken);

        KnowledgeEmbeddingBatch queryEmbedding =
            await embeddingService.GenerateAsync([query], cancellationToken);
        List<SearchCandidate> vector = [];
        if (queryEmbedding.Available)
        {
            vector = await SearchVectorAsync(
                queryEmbedding.Vectors[0],
                queryEmbedding.Model!,
                candidateLimit,
                cancellationToken);
        }

        SearchCandidate[] candidates = fullText
            .Concat(fuzzy)
            .Concat(vector)
            .DistinctBy(item => item.ChunkId)
            .ToArray();
        IReadOnlyDictionary<long, double> scores = ReciprocalRankFusion.Combine(
        [
            fullText.Select(item => item.ChunkId).ToArray(),
            fuzzy.Select(item => item.ChunkId).ToArray(),
            vector.Select(item => item.ChunkId).ToArray(),
        ],
            RrfConstant);

        KnowledgeSearchResult[] results = candidates
            .OrderByDescending(item => scores[item.ChunkId])
            .ThenBy(item => item.ChunkId)
            .Take(limit)
            .Select(item => item.ToResult(scores[item.ChunkId]))
            .ToArray();

        return new KnowledgeSearchResponse(
            query,
            queryEmbedding.Available
                ? KnowledgeSearchMode.Hybrid
                : KnowledgeSearchMode.LexicalFallback,
            results,
            queryEmbedding.Error);
    }

    private async Task<List<SearchCandidate>> SearchFullTextAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        return await dbContext.KnowledgeChunks
            .AsNoTracking()
            .Where(
                chunk => chunk.SearchVector.Matches(
                    EF.Functions.WebSearchToTsQuery("simple", query)))
            .OrderByDescending(
                chunk => chunk.SearchVector.RankCoverDensity(
                    EF.Functions.WebSearchToTsQuery("simple", query)))
            .Take(limit)
            .Select(chunk => new SearchCandidate(
                chunk.Id,
                chunk.DocumentId,
                chunk.Document.FileName,
                chunk.Content,
                chunk.Ordinal,
                chunk.PageNumber,
                chunk.SheetName,
                chunk.Section))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<SearchCandidate>> SearchFuzzyAsync(
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        return await dbContext.KnowledgeChunks
            .AsNoTracking()
            .Where(
                chunk => EF.Functions.TrigramsSimilarity(
                    chunk.NormalizedContent,
                    query) > 0.12)
            .OrderByDescending(
                chunk => EF.Functions.TrigramsSimilarity(
                    chunk.NormalizedContent,
                    query))
            .Take(limit)
            .Select(chunk => new SearchCandidate(
                chunk.Id,
                chunk.DocumentId,
                chunk.Document.FileName,
                chunk.Content,
                chunk.Ordinal,
                chunk.PageNumber,
                chunk.SheetName,
                chunk.Section))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<SearchCandidate>> SearchVectorAsync(
        ReadOnlyMemory<float> embedding,
        string model,
        int limit,
        CancellationToken cancellationToken)
    {
        Vector queryVector = new(embedding);
        return await dbContext.KnowledgeEmbeddings
            .AsNoTracking()
            .Where(item => item.Model == model)
            .OrderBy(item => item.Value.CosineDistance(queryVector))
            .Take(limit)
            .Select(item => new SearchCandidate(
                item.Chunk.Id,
                item.Chunk.DocumentId,
                item.Chunk.Document.FileName,
                item.Chunk.Content,
                item.Chunk.Ordinal,
                item.Chunk.PageNumber,
                item.Chunk.SheetName,
                item.Chunk.Section))
            .ToListAsync(cancellationToken);
    }

    private sealed record SearchCandidate(
        long ChunkId,
        Guid DocumentId,
        string FileName,
        string Content,
        int Ordinal,
        int? PageNumber,
        string? SheetName,
        string? Section)
    {
        public KnowledgeSearchResult ToResult(double score) =>
            new(
                ChunkId,
                DocumentId,
                FileName,
                Content,
                Ordinal,
                PageNumber,
                SheetName,
                Section,
                score);
    }
}
