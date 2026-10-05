using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ollama.Configuration;

namespace ollama;

public interface IOllamaModelCatalog
{
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default);
}

public sealed class OllamaModelCatalog(IHttpClientFactory httpClientFactory) : IOllamaModelCatalog
{
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(nameof(OllamaModelCatalog));
        var response = await client.GetFromJsonAsync<OllamaTagsResponse>("/api/tags", cancellationToken);

        return response?.Models?
            .Select(model => model.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
    }

    private sealed class OllamaTagsResponse
    {
        [JsonPropertyName("models")]
        public List<OllamaModelInfo>? Models { get; init; }
    }

    private sealed class OllamaModelInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }
}
