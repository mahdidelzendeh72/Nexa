using System.Text.Json;
using System.Text.Json.Serialization;
using Maf.Agent.Api.Models.Application;
using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Models.Infrastructure;

public sealed class OpenAiModelListResponse
{
    public List<OpenAiModelResponse>? Data { get; init; }
}

public sealed class OpenAiModelResponse
{
    public string? Id { get; init; }

    public long? Created { get; init; }

    [JsonPropertyName("context_length")]
    public int? ContextLength { get; init; }

    public List<string>? Capabilities { get; init; }
}

public sealed class OpenAiModelCatalog(IHttpClientFactory httpClientFactory) : IModelCatalog
{
    public bool Supports(ModelProviderKind kind) =>
        kind is ModelProviderKind.OpenAI or ModelProviderKind.OpenAICompatible;

    public async Task<IReadOnlyList<DiscoveredModel>> ListAsync(
        ModelProviderConnection connection,
        CancellationToken cancellationToken)
    {
        OpenAiModelListResponse response = await ModelProviderHttp.SendAsync<OpenAiModelListResponse>(
            httpClientFactory,
            connection,
            HttpMethod.Get,
            ModelsUri(connection.BaseAddress),
            body: null,
            cancellationToken);
        try
        {
            return Map(response);
        }
        catch (JsonException exception)
        {
            throw new ModelDiscoveryException(
                $"پاسخ مدل‌های «{connection.Name}» قابل خواندن نبود.",
                exception);
        }
    }

    public static IReadOnlyList<DiscoveredModel> Read(string json) =>
        Map(JsonSerializer.Deserialize<OpenAiModelListResponse>(json, ModelProviderHttp.Json)
            ?? throw new JsonException("OpenAI model list has no data array."));

    public static IReadOnlyList<DiscoveredModel> Map(OpenAiModelListResponse response)
    {
        if (response.Data is null)
        {
            throw new JsonException("OpenAI model list has no data array.");
        }

        List<DiscoveredModel> models = [];
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (OpenAiModelResponse item in response.Data)
        {
            if (string.IsNullOrWhiteSpace(item.Id) || !names.Add(item.Id))
            {
                continue;
            }

            models.Add(new DiscoveredModel(
                item.Id,
                UnixTime(item.Created),
                item.ContextLength ?? OpenAiModelCapabilityCatalog.ContextLength(item.Id),
                OpenAiModelCapabilityCatalog.Get(item.Id) | ModelCapabilities.From(item.Capabilities)));
        }

        return models;
    }

    public static Uri ServiceRoot(Uri baseAddress)
    {
        string absolute = ModelsUri(baseAddress).AbsoluteUri;
        const string suffix = "/models";
        return new Uri(absolute[..^suffix.Length]);
    }

    public static Uri ModelsUri(Uri baseAddress)
    {
        string root = baseAddress.AbsoluteUri.TrimEnd('/');
        if (!root.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            root += "/v1";
        }

        return new Uri(root + "/models");
    }

    private static DateTimeOffset? UnixTime(long? seconds)
    {
        if (seconds is null)
        {
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(seconds.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }
}
