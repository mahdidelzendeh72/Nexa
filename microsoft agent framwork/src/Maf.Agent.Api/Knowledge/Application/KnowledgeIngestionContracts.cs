namespace Maf.Agent.Api.Knowledge.Application;

public sealed record ExtractedSection(
    string Content,
    int? PageNumber = null,
    string? SheetName = null,
    string? Section = null);

public sealed record KnowledgeChunkDraft(
    int Ordinal,
    string Content,
    string NormalizedContent,
    int TokenEstimate,
    int? PageNumber,
    string? SheetName,
    string? Section);

public interface IDocumentTextExtractor
{
    bool CanExtract(string fileName, string contentType);

    Task<IReadOnlyList<ExtractedSection>> ExtractAsync(
        Stream content,
        CancellationToken cancellationToken);
}

public interface ITextNormalizer
{
    string Normalize(string text);
}

public interface IKnowledgeChunker
{
    IReadOnlyList<KnowledgeChunkDraft> CreateChunks(
        IReadOnlyList<ExtractedSection> sections);
}

public interface IKnowledgeIngestionQueue
{
    ValueTask EnqueueAsync(Guid documentId, CancellationToken cancellationToken);
    IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken);
}
