namespace Maf.Agent.Api.Knowledge.Application;

public enum KnowledgeSearchMode
{
    LexicalFallback,
    Hybrid,
}

public sealed record KnowledgeSearchResult(
    long ChunkId,
    Guid DocumentId,
    string FileName,
    string Content,
    int Ordinal,
    int? PageNumber,
    string? SheetName,
    string? Section,
    double Score);

public sealed record KnowledgeSearchResponse(
    string Query,
    KnowledgeSearchMode Mode,
    IReadOnlyList<KnowledgeSearchResult> Results,
    string? EmbeddingError = null);

public interface IKnowledgeRetriever
{
    Task<KnowledgeSearchResponse> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken);
}
