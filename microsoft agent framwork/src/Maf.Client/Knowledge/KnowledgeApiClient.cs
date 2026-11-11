using System.Net.Http.Json;
using Microsoft.JSInterop;

namespace Maf.Client.Knowledge;

public sealed class KnowledgeApiClient(
    IHttpClientFactory httpClientFactory,
    IJSRuntime jsRuntime)
{
    private HttpClient Client => httpClientFactory.CreateClient("Backend");

    public async Task<KnowledgeDocument[]> ListDocumentsAsync(
        CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<KnowledgeDocument[]>(
            "api/knowledge/documents",
            cancellationToken) ?? [];

    public async Task<KnowledgeUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using MultipartFormDataContent form = [];
        using StreamContent file = new(content);
        file.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);

        using HttpResponseMessage response = await Client.PostAsync(
            "api/knowledge/documents",
            form,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<KnowledgeUploadResult>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("پاسخ بارگذاری معتبر نبود.");
    }

    public async Task RetryAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PostAsync(
            $"api/knowledge/documents/{documentId}/retry",
            content: null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task ReindexAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PostAsync(
            $"api/knowledge/documents/{documentId}/reindex",
            content: null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.DeleteAsync(
            $"api/knowledge/documents/{documentId}",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task DownloadAsync(
        KnowledgeDocument document,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.GetAsync(
            $"api/knowledge/documents/{document.Id}/download",
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        await using Stream stream =
            await response.Content.ReadAsStreamAsync(cancellationToken);
        using DotNetStreamReference reference = new(stream);
        await jsRuntime.InvokeVoidAsync(
            "maf.downloadFile",
            cancellationToken,
            document.FileName,
            document.ContentType,
            reference);
    }

    public async Task<KnowledgeSearchResponse> SearchAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PostAsJsonAsync(
            "api/knowledge/search",
            new { query, limit },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<KnowledgeSearchResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("پاسخ جست‌وجو معتبر نبود.");
    }

    public async Task<KnowledgeHealth> GetHealthAsync(
        CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<KnowledgeHealth>(
            "api/knowledge/health",
            cancellationToken)
            ?? throw new InvalidOperationException("پاسخ سلامت معتبر نبود.");

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string details = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(details)
                ? $"درخواست با کد {(int)response.StatusCode} ناموفق بود."
                : details,
            inner: null,
            response.StatusCode);
    }
}
