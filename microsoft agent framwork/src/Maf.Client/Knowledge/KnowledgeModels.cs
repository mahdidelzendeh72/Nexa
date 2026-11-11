namespace Maf.Client.Knowledge;

public sealed record KnowledgeDocument(
    Guid Id,
    string FileName,
    string ContentType,
    long Size,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? Error,
    int ChunkCount,
    string? EmbeddingModel);

public sealed record KnowledgeUploadResult(
    Guid DocumentId,
    bool AlreadyExisted);

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
    KnowledgeSearchResult[] Results,
    string? EmbeddingError);

public sealed record KnowledgeHealth(
    string Database,
    string[] Extensions,
    KnowledgeEmbeddingHealth Embedding);

public sealed record KnowledgeEmbeddingHealth(
    string Status,
    string? Provider,
    string? Model,
    string? Error);
