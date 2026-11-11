using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Maf.Agent.Api.Models.Application;
using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Models.Infrastructure;

public sealed class OllamaTagsResponse
{
    public List<OllamaModelResponse>? Models { get; init; }
}

public sealed class OllamaModelResponse
{
    public string? Name { get; init; }

    public string? Model { get; init; }

    [JsonPropertyName("modified_at")]
    public DateTimeOffset? ModifiedAt { get; init; }

    public OllamaModelDetailsResponse? Details { get; init; }
}

public sealed class OllamaModelDetailsResponse
{
    public List<string>? Families { get; init; }
}

public sealed class OllamaShowRequest
{
    public required string Model { get; init; }
}

public sealed class OllamaShowResponse
{
    [JsonPropertyName("modified_at")]
    public DateTimeOffset? ModifiedAt { get; init; }

    public List<string>? Capabilities { get; init; }

    [JsonPropertyName("model_info")]
    public JsonObject? ModelInfo { get; init; }
}

public sealed class OllamaModelCatalog(IHttpClientFactory httpClientFactory) : IModelCatalog
{
    public bool Supports(ModelProviderKind kind) => kind == ModelProviderKind.Ollama;

    public async Task<IReadOnlyList<DiscoveredModel>> ListAsync(
        ModelProviderConnection connection,
        CancellationToken cancellationToken)
    {
        Uri root = Root(connection.BaseAddress);
        OllamaTagsResponse tags = await ModelProviderHttp.SendAsync<OllamaTagsResponse>(
            httpClientFactory,
            connection,
            HttpMethod.Get,
            new Uri(root, "api/tags"),
            body: null,
            cancellationToken);
        IReadOnlyList<DiscoveredModel> listed;
        try
        {
            listed = MapTags(tags);
        }
        catch (JsonException exception)
        {
            throw new ModelDiscoveryException(
                $"پاسخ مدل‌های «{connection.Name}» قابل خواندن نبود.",
                exception);
        }

        List<DiscoveredModel> models = [];
        foreach (DiscoveredModel model in listed)
        {
            try
            {
                OllamaShowResponse show = await ModelProviderHttp.SendAsync<OllamaShowResponse>(
                    httpClientFactory,
                    connection,
                    HttpMethod.Post,
                    new Uri(root, "api/show"),
                    new OllamaShowRequest { Model = model.Name },
                    cancellationToken);
                models.Add(MapShow(model, show));
            }
            catch (ModelDiscoveryException)
            {
                models.Add(model);
            }
            catch (JsonException)
            {
                models.Add(model);
            }
        }

        return models;
    }

    public static IReadOnlyList<DiscoveredModel> ReadTags(string json) =>
        MapTags(JsonSerializer.Deserialize<OllamaTagsResponse>(json, ModelProviderHttp.Json)
            ?? throw new JsonException("Ollama model list has no models array."));

    public static DiscoveredModel ReadShow(DiscoveredModel listed, string json) =>
        MapShow(
            listed,
            JsonSerializer.Deserialize<OllamaShowResponse>(json, ModelProviderHttp.Json)
                ?? throw new JsonException("Ollama model details are empty."));

    public static IReadOnlyList<DiscoveredModel> MapTags(OllamaTagsResponse response)
    {
        if (response.Models is null)
        {
            throw new JsonException("Ollama model list has no models array.");
        }

        List<DiscoveredModel> models = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (OllamaModelResponse item in response.Models)
        {
            string? name = string.IsNullOrWhiteSpace(item.Name) ? item.Model : item.Name;
            if (string.IsNullOrWhiteSpace(name) || !names.Add(name))
            {
                continue;
            }

            models.Add(new DiscoveredModel(
                name,
                item.ModifiedAt,
                null,
                OllamaModelCapabilityCatalog.Get(item)));
        }

        return models;
    }

    public static DiscoveredModel MapShow(DiscoveredModel listed, OllamaShowResponse response) =>
        new(
            listed.Name,
            response.ModifiedAt ?? listed.LastChangedAt,
            ContextLength(response.ModelInfo) ?? listed.MaxTokens,
            listed.Capabilities | ModelCapabilities.From(response.Capabilities));

    public static Uri Root(Uri baseAddress)
    {
        string root = baseAddress.AbsoluteUri.TrimEnd('/');
        if (root.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            root = root[..^3].TrimEnd('/');
        }

        return new Uri(root + "/");
    }

    private static int? ContextLength(JsonObject? info)
    {
        if (info is null)
        {
            return null;
        }

        int? length = null;
        foreach (KeyValuePair<string, JsonNode?> property in info)
        {
            if (!property.Key.EndsWith("context_length", StringComparison.Ordinal) ||
                property.Value is not JsonValue value ||
                !value.TryGetValue<int>(out int number) ||
                number <= 0 ||
                (length is not null && number <= length))
            {
                continue;
            }

            length = number;
        }

        return length;
    }
}
