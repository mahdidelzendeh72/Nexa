using System.Net.Http.Json;
using Maf.Agent.Api.Knowledge.Application;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace Maf.Agent.Api.Knowledge.Infrastructure;

public sealed class KnowledgeEmbeddingOptions
{
    public string Provider { get; set; } = "None";
    public string Endpoint { get; set; } = "http://localhost:11434/";
    public string Model { get; set; } = "bge-m3";
    public int Dimensions { get; set; } = 1024;
}

public sealed class OllamaEmbeddingGenerator(
    HttpClient httpClient,
    IOptions<KnowledgeEmbeddingOptions> options)
    : IEmbeddingGenerator<string, Embedding<float>>
{
    private readonly KnowledgeEmbeddingOptions _options = options.Value;

    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        string[] inputs = values.ToArray();
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(
            "api/embed",
            new OllamaEmbeddingRequest(_options.Model, inputs),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        OllamaEmbeddingResponse result =
            await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>(
                cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException(
                "Ollama returned an empty embedding response.");

        if (result.Embeddings.Length != inputs.Length)
        {
            throw new InvalidOperationException(
                "Ollama returned a different number of embeddings than requested.");
        }

        if (result.Embeddings.Any(vector => vector.Length != _options.Dimensions))
        {
            throw new InvalidOperationException(
                $"Ollama model '{_options.Model}' must return {_options.Dimensions} dimensions.");
        }

        return new GeneratedEmbeddings<Embedding<float>>(
            result.Embeddings.Select(vector => new Embedding<float>(vector)
            {
                ModelId = result.Model,
                CreatedAt = DateTimeOffset.UtcNow,
            }));
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceType == typeof(EmbeddingGeneratorMetadata))
        {
            return new EmbeddingGeneratorMetadata(
                "Ollama",
                httpClient.BaseAddress,
                _options.Model,
                _options.Dimensions);
        }

        return serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        // The HttpClient lifetime is managed by IHttpClientFactory.
    }

    private sealed record OllamaEmbeddingRequest(string Model, string[] Input);

    private sealed record OllamaEmbeddingResponse(
        string Model,
        float[][] Embeddings);
}

public sealed class KnowledgeEmbeddingService(
    IOptions<KnowledgeEmbeddingOptions> options,
    IEmbeddingGenerator<string, Embedding<float>> generator,
    ILogger<KnowledgeEmbeddingService> logger)
    : IKnowledgeEmbeddingService
{
    private readonly KnowledgeEmbeddingOptions _options = options.Value;

    public async Task<KnowledgeEmbeddingBatch> GenerateAsync(
        IReadOnlyList<string> inputs,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return new KnowledgeEmbeddingBatch(false, null, []);
        }

        try
        {
            GeneratedEmbeddings<Embedding<float>> generated =
                await generator.GenerateAsync(inputs, cancellationToken: cancellationToken);
            return new KnowledgeEmbeddingBatch(
                true,
                _options.Model,
                generated.Select(embedding => embedding.Vector).ToArray());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidOperationException)
        {
            logger.LogWarning(
                exception,
                "Embedding generation is unavailable; lexical retrieval remains active.");
            return new KnowledgeEmbeddingBatch(
                false,
                _options.Model,
                [],
                exception.Message);
        }
    }

    public async Task<KnowledgeEmbeddingHealth> GetHealthAsync(
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return new KnowledgeEmbeddingHealth("Disabled", "None", null);
        }

        KnowledgeEmbeddingBatch probe =
            await GenerateAsync(["health"], cancellationToken);
        return probe.Available
            ? new KnowledgeEmbeddingHealth("Healthy", "Ollama", _options.Model)
            : new KnowledgeEmbeddingHealth(
                "Unavailable",
                "Ollama",
                _options.Model,
                probe.Error);
    }

    private bool IsEnabled =>
        _options.Provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase);
}
