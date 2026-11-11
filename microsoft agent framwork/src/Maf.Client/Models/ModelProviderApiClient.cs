using System.Net.Http.Json;
using System.Text.Json;

namespace Maf.Client.Models;

public sealed class ModelProviderApiClient(IHttpClientFactory httpClientFactory)
{
    private HttpClient Client => httpClientFactory.CreateClient("Backend");

    public async Task<ModelProviderSummary[]> ListAsync(
        CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<ModelProviderSummary[]>(
            "api/model-providers",
            cancellationToken) ?? [];

    public async Task<ModelProviderDetails> GetAsync(
        Guid providerId,
        CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<ModelProviderDetails>(
            $"api/model-providers/{providerId}",
            cancellationToken)
        ?? throw new InvalidOperationException("پاسخ ارائه‌دهنده مدل معتبر نبود.");

    public async Task<ModelProviderDetails> CreateAsync(
        SaveModelProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PostAsJsonAsync(
            "api/model-providers",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<ModelProviderDetails>(response, cancellationToken);
    }

    public async Task<ModelProviderDetails> UpdateAsync(
        Guid providerId,
        SaveModelProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PutAsJsonAsync(
            $"api/model-providers/{providerId}",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<ModelProviderDetails>(response, cancellationToken);
    }

    public async Task<ModelDefaultsResponse> GetDefaultsAsync(
        CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<ModelDefaultsResponse>(
            "api/model-providers/defaults",
            cancellationToken)
        ?? throw new InvalidOperationException("پاسخ مدل‌های پیش‌فرض معتبر نبود.");

    public async Task<ModelDefaultsResponse> SaveDefaultsAsync(
        SaveModelDefaultsRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PutAsJsonAsync(
            "api/model-providers/defaults",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<ModelDefaultsResponse>(response, cancellationToken);
    }

    public async Task DeleteAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.DeleteAsync(
            $"api/model-providers/{providerId}",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<ModelProviderDetails> DiscoverAsync(
        Guid providerId,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PostAsync(
            $"api/model-providers/{providerId}/discover",
            content: null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<ModelProviderDetails>(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
        ?? throw new InvalidOperationException("پاسخ ارائه‌دهنده مدل معتبر نبود.");

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        string message = body;
        try
        {
            using JsonDocument document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out JsonElement error))
            {
                message = error.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
        }

        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(message)
                ? $"درخواست با کد {(int)response.StatusCode} ناموفق بود."
                : message,
            inner: null,
            response.StatusCode);
    }
}
