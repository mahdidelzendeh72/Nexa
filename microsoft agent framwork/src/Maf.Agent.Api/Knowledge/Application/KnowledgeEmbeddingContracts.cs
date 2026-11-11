using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Knowledge.Application;

public sealed record KnowledgeEmbeddingBatch(
    bool Available,
    string? Model,
    IReadOnlyList<ReadOnlyMemory<float>> Vectors,
    string? Error = null);

public sealed record KnowledgeEmbeddingHealth(
    string Status,
    string? Provider,
    string? Model,
    string? Error = null);

public interface IKnowledgeEmbeddingService
{
    Task<KnowledgeEmbeddingBatch> GenerateAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken);

    Task<KnowledgeEmbeddingHealth> GetHealthAsync(
        CancellationToken cancellationToken);
}
