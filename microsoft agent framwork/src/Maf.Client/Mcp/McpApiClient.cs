using System.Net.Http.Json;
using System.Text.Json;

namespace Maf.Client.Mcp;

public sealed class McpApiClient(IHttpClientFactory httpClientFactory)
{
    private HttpClient Client => httpClientFactory.CreateClient("Backend");

    public async Task<McpServerSummary[]> ListAsync(
        CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<McpServerSummary[]>(
            "api/mcp/servers",
            cancellationToken) ?? [];

    public async Task<McpServerDetails> GetAsync(
        Guid serverId,
        CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<McpServerDetails>(
            $"api/mcp/servers/{serverId}",
            cancellationToken)
        ?? throw new InvalidOperationException("پاسخ سرور MCP معتبر نبود.");

    public async Task<McpServerDetails> CreateAsync(
        SaveMcpServerRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PostAsJsonAsync(
            "api/mcp/servers",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<McpServerDetails>(response, cancellationToken);
    }

    public async Task<McpServerDetails> UpdateAsync(
        Guid serverId,
        SaveMcpServerRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PutAsJsonAsync(
            $"api/mcp/servers/{serverId}",
            request,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<McpServerDetails>(response, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.DeleteAsync(
            $"api/mcp/servers/{serverId}",
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<McpServerDetails> SaveToolApprovalsAsync(
        Guid serverId,
        IReadOnlyList<McpToolApprovalRequest> approvals,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PutAsJsonAsync(
            $"api/mcp/servers/{serverId}/tool-approvals",
            approvals,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<McpServerDetails>(response, cancellationToken);
    }

    public async Task<McpServerDetails> DiscoverAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PostAsync(
            $"api/mcp/servers/{serverId}/discover",
            content: null,
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync<McpServerDetails>(response, cancellationToken);
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
        ?? throw new InvalidOperationException("پاسخ سرور MCP معتبر نبود.");

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
