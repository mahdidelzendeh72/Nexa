using Maf.Agent.Api.Knowledge.Domain;

namespace Maf.Agent.Api.Tests;

public sealed class KnowledgeDomainTests
{
    [Fact]
    public void Document_controls_its_processing_lifecycle()
    {
        DateTimeOffset createdAt = DateTimeOffset.UtcNow;
        KnowledgeDocument document = CreateDocument(createdAt);

        document.StartProcessing(createdAt.AddMinutes(1));
        document.CompleteProcessing(
            chunkCount: 3,
            embeddingModel: null,
            createdAt.AddMinutes(2));

        Assert.Equal(
            KnowledgeDocumentStatus.ReadyWithoutEmbedding,
            document.Status);
        Assert.Equal(3, document.ChunkCount);
        Assert.Null(document.Error);
        Assert.NotNull(document.CompletedAt);
    }

    [Fact]
    public void Document_rejects_completion_before_processing()
    {
        KnowledgeDocument document = CreateDocument(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            document.CompleteProcessing(
                1,
                embeddingModel: null,
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Processing_attempt_enforces_stage_order()
    {
        KnowledgeProcessingAttempt attempt = KnowledgeProcessingAttempt.Start(
            Guid.NewGuid(),
            attemptNumber: 1,
            DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(attempt.BeginEmbedding);

        attempt.BeginChunking();
        attempt.BeginEmbedding();
        attempt.Complete(DateTimeOffset.UtcNow);

        Assert.Equal(KnowledgeProcessingStage.Completed, attempt.Stage);
    }

    private static KnowledgeDocument CreateDocument(DateTimeOffset createdAt) =>
        KnowledgeDocument.Create(
            Guid.NewGuid(),
            "knowledge.txt",
            "text/plain",
            size: 10,
            new string('a', 64),
            "user-id",
            createdAt);
}
