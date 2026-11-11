using System.Net.Http.Json;

namespace Maf.Client.Chat;

public sealed class ChatTraceApiClient(IHttpClientFactory httpClientFactory)
{
    private HttpClient Client => httpClientFactory.CreateClient("Backend");

    public async Task<ChatTraceThreadView[]> ListThreadsAsync(CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<ChatTraceThreadView[]>("api/chat/traces", cancellationToken) ?? [];

    public async Task<ChatTraceRunView[]> GetThreadAsync(
        string threadId,
        CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<ChatTraceRunView[]>(
            $"api/chat/traces/{Uri.EscapeDataString(threadId)}",
            cancellationToken) ?? [];
}