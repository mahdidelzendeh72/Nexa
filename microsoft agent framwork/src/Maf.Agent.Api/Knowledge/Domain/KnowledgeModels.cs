using NpgsqlTypes;
using Pgvector;

namespace Maf.Agent.Api.Knowledge.Domain;

public enum KnowledgeDocumentStatus
{
    Pending,
    Processing,
    Ready,
    ReadyWithoutEmbedding,
    Failed,
}

public enum KnowledgeProcessingStage
{
    Queued,
    Extracting,
    Normalizing,
    Chunking,
    LexicalIndexing,
    Embedding,
    Completed,
    Failed,
}

public sealed class KnowledgeDocument
{
    private readonly List<KnowledgeChunk> _chunks = [];
    private readonly List<KnowledgeProcessingAttempt> _attempts = [];

    private KnowledgeDocument()
    {
    }

    private KnowledgeDocument(
        Guid id,
        string fileName,
        string contentType,
        long size,
        string sha256,
        string createdByUserId,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdByUserId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (sha256.Length != 64)
        {
            throw new ArgumentException(
                "A SHA-256 hexadecimal digest must contain exactly 64 characters.",
                nameof(sha256));
        }

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        FileName = fileName;
        ContentType = contentType;
        Size = size;
        Sha256 = sha256;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
        Status = KnowledgeDocumentStatus.Pending;
    }

    public Guid Id { get; private set; }
    public string FileName { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long Size { get; private set; }
    public string Sha256 { get; private set; } = null!;
    public KnowledgeDocumentStatus Status { get; private set; }
    public string CreatedByUserId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Error { get; private set; }
    public int ChunkCount { get; private set; }
    public string? EmbeddingModel { get; private set; }

    public KnowledgeDocumentContent? OriginalContent { get; private set; }
    public IReadOnlyCollection<KnowledgeChunk> Chunks => _chunks;
    public IReadOnlyCollection<KnowledgeProcessingAttempt> Attempts => _attempts;

    public static KnowledgeDocument Create(
        Guid id,
        string fileName,
        string contentType,
        long size,
        string sha256,
        string createdByUserId,
        DateTimeOffset createdAt) =>
        new(
            id,
            fileName,
            contentType,
            size,
            sha256,
            createdByUserId,
            createdAt);

    public void StartProcessing(DateTimeOffset startedAt)
    {
        if (Status is not KnowledgeDocumentStatus.Pending and
            not KnowledgeDocumentStatus.Processing)
        {
            throw new InvalidOperationException(
                $"A document in '{Status}' state cannot start processing.");
        }

        Status = KnowledgeDocumentStatus.Processing;
        ProcessingStartedAt = startedAt;
        CompletedAt = null;
        Error = null;
    }

    public void ScheduleRetry()
    {
        if (Status == KnowledgeDocumentStatus.Processing)
        {
            throw new InvalidOperationException(
                "A document that is currently processing cannot be queued again.");
        }

        Status = KnowledgeDocumentStatus.Pending;
        ProcessingStartedAt = null;
        CompletedAt = null;
        Error = null;
    }

    public void CompleteProcessing(
        int chunkCount,
        string? embeddingModel,
        DateTimeOffset completedAt)
    {
        EnsureProcessing();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkCount);

        ChunkCount = chunkCount;
        EmbeddingModel = string.IsNullOrWhiteSpace(embeddingModel)
            ? null
            : embeddingModel;
        Status = EmbeddingModel is null
            ? KnowledgeDocumentStatus.ReadyWithoutEmbedding
            : KnowledgeDocumentStatus.Ready;
        CompletedAt = completedAt;
        Error = null;
    }

    public void FailProcessing(string error, DateTimeOffset failedAt)
    {
        EnsureProcessing();
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        Status = KnowledgeDocumentStatus.Failed;
        Error = error;
        CompletedAt = failedAt;
    }

    private void EnsureProcessing()
    {
        if (Status != KnowledgeDocumentStatus.Processing)
        {
            throw new InvalidOperationException(
                $"A document in '{Status}' state is not being processed.");
        }
    }
}

public sealed class KnowledgeDocumentContent
{
    private KnowledgeDocumentContent()
    {
    }

    public Guid DocumentId { get; private set; }
    public byte[] Data { get; private set; } = [];
    public KnowledgeDocument Document { get; private set; } = null!;
}

public sealed class KnowledgeChunk
{
    private KnowledgeChunk()
    {
    }

    private KnowledgeChunk(
        Guid documentId,
        int ordinal,
        string content,
        string normalizedContent,
        int tokenEstimate,
        int? pageNumber,
        string? sheetName,
        string? section)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document ID is required.", nameof(documentId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedContent);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tokenEstimate);

        DocumentId = documentId;
        Ordinal = ordinal;
        Content = content;
        NormalizedContent = normalizedContent;
        TokenEstimate = tokenEstimate;
        PageNumber = pageNumber;
        SheetName = sheetName;
        Section = section;
    }

    public long Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public int Ordinal { get; private set; }
    public string Content { get; private set; } = null!;
    public string NormalizedContent { get; private set; } = null!;
    public int TokenEstimate { get; private set; }
    public int? PageNumber { get; private set; }
    public string? SheetName { get; private set; }
    public string? Section { get; private set; }
    public NpgsqlTsVector SearchVector { get; private set; } = null!;

    public KnowledgeDocument Document { get; private set; } = null!;
    public KnowledgeEmbedding? Embedding { get; private set; }

    public static KnowledgeChunk Create(
        Guid documentId,
        int ordinal,
        string content,
        string normalizedContent,
        int tokenEstimate,
        int? pageNumber,
        string? sheetName,
        string? section) =>
        new(
            documentId,
            ordinal,
            content,
            normalizedContent,
            tokenEstimate,
            pageNumber,
            sheetName,
            section);
}

public sealed class KnowledgeEmbedding
{
    private const int RequiredDimensions = 1024;

    private KnowledgeEmbedding()
    {
    }

    private KnowledgeEmbedding(
        long chunkId,
        string model,
        ReadOnlyMemory<float> value,
        DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(chunkId);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        if (value.Length != RequiredDimensions)
        {
            throw new ArgumentException(
                $"Knowledge embeddings must have {RequiredDimensions} dimensions.",
                nameof(value));
        }

        ChunkId = chunkId;
        Model = model;
        Dimensions = value.Length;
        Value = new Vector(value);
        CreatedAt = createdAt;
    }

    public long ChunkId { get; private set; }
    public string Model { get; private set; } = null!;
    public int Dimensions { get; private set; }
    public Vector Value { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    public KnowledgeChunk Chunk { get; private set; } = null!;

    public static KnowledgeEmbedding Create(
        long chunkId,
        string model,
        ReadOnlyMemory<float> value,
        DateTimeOffset createdAt) =>
        new(chunkId, model, value, createdAt);
}

public sealed class KnowledgeProcessingAttempt
{
    private KnowledgeProcessingAttempt()
    {
    }

    private KnowledgeProcessingAttempt(
        Guid documentId,
        int attemptNumber,
        DateTimeOffset startedAt)
    {
        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document ID is required.", nameof(documentId));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptNumber);
        DocumentId = documentId;
        AttemptNumber = attemptNumber;
        Stage = KnowledgeProcessingStage.Extracting;
        StartedAt = startedAt;
    }

    public long Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public int AttemptNumber { get; private set; }
    public KnowledgeProcessingStage Stage { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Error { get; private set; }

    public KnowledgeDocument Document { get; private set; } = null!;

    public static KnowledgeProcessingAttempt Start(
        Guid documentId,
        int attemptNumber,
        DateTimeOffset startedAt) =>
        new(documentId, attemptNumber, startedAt);

    public void BeginChunking() =>
        MoveTo(KnowledgeProcessingStage.Extracting, KnowledgeProcessingStage.Chunking);

    public void BeginEmbedding() =>
        MoveTo(KnowledgeProcessingStage.Chunking, KnowledgeProcessingStage.Embedding);

    public void Complete(DateTimeOffset completedAt)
    {
        MoveTo(KnowledgeProcessingStage.Embedding, KnowledgeProcessingStage.Completed);
        CompletedAt = completedAt;
        Error = null;
    }

    public void Fail(Exception exception, DateTimeOffset failedAt)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (Stage is KnowledgeProcessingStage.Completed or KnowledgeProcessingStage.Failed)
        {
            throw new InvalidOperationException(
                $"A processing attempt in '{Stage}' state cannot fail.");
        }

        Stage = KnowledgeProcessingStage.Failed;
        Error = exception.ToString();
        CompletedAt = failedAt;
    }

    private void MoveTo(
        KnowledgeProcessingStage expected,
        KnowledgeProcessingStage next)
    {
        if (Stage != expected)
        {
            throw new InvalidOperationException(
                $"Cannot move a processing attempt from '{Stage}' to '{next}'.");
        }

        Stage = next;
    }
}
