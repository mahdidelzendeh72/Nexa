using Maf.Agent.Api.Knowledge.Application;
using Maf.Agent.Api.Knowledge.Endpoints;
using Maf.Agent.Api.Knowledge.Infrastructure;
using Maf.Agent.Api.Knowledge.Infrastructure.Extractors;
using Microsoft.Extensions.AI;

namespace Maf.Agent.Api.Knowledge;

public static class KnowledgeFeature
{
    public static IServiceCollection AddKnowledgeFeature(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddKnowledgeApi();
        services
            .AddOptions<OcrOptions>()
            .Bind(configuration.GetSection("Knowledge:Ocr"))
            .Validate(
                options => options.Dpi > 0,
                "Knowledge:Ocr:Dpi must be greater than zero.")
            .ValidateOnStart();
        services
            .AddOptions<KnowledgeEmbeddingOptions>()
            .Bind(configuration.GetSection("Knowledge:Embedding"))
            .Validate(
                options => options.Dimensions == 1024,
                "Knowledge embeddings must have exactly 1024 dimensions.")
            .ValidateOnStart();

        services.AddSingleton<ITextNormalizer, PersianTextNormalizer>();
        services.AddSingleton<IKnowledgeChunker, StructureAwareKnowledgeChunker>();
        services.AddSingleton<IKnowledgeIngestionQueue, KnowledgeIngestionQueue>();
        services.AddScoped<IKnowledgeDocumentStorage, PostgresKnowledgeDocumentStorage>();
        services.AddSingleton<IDocumentTextExtractor, PlainTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, DocxTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, XlsxTextExtractor>();
        services.AddSingleton<IDocumentTextExtractor, PdfTextExtractor>();
        services.AddScoped<IKnowledgeEmbeddingService, KnowledgeEmbeddingService>();
        services.AddScoped<IKnowledgeRetriever, PostgresHybridKnowledgeRetriever>();
        services.AddHostedService<KnowledgeIngestionWorker>();

        services.AddHttpClient<OllamaEmbeddingGenerator>((provider, client) =>
        {
            KnowledgeEmbeddingOptions options = provider
                .GetRequiredService<
                    Microsoft.Extensions.Options.IOptions<KnowledgeEmbeddingOptions>>()
                .Value;
            client.BaseAddress = new Uri(options.Endpoint);
            client.Timeout = TimeSpan.FromMinutes(5);
        });
        services.AddScoped<IEmbeddingGenerator<string, Embedding<float>>>(
            provider => provider.GetRequiredService<OllamaEmbeddingGenerator>());

        return services;
    }

    public static IEndpointRouteBuilder MapKnowledgeFeature(
        this IEndpointRouteBuilder endpoints) =>
        endpoints.MapKnowledgeEndpoints();
}
