using System.Net.Http.Json;
using System.Text.Json;

namespace Maf.Client.Identity;

public sealed class AccountApiClient(IHttpClientFactory httpClientFactory)
{
    private HttpClient Client => httpClientFactory.CreateClient("Backend");

    public async Task<AccountRoleChoice[]> RolesAsync(CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<AccountRoleChoice[]>("api/users/roles", cancellationToken) ?? [];

    public async Task<AccountUserResponse[]> ListAsync(CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<AccountUserResponse[]>("api/users", cancellationToken) ?? [];

    public async Task<AccountUserResponse> GetAsync(string userId, CancellationToken cancellationToken = default) =>
        await Client.GetFromJsonAsync<AccountUserResponse>($"api/users/{userId}", cancellationToken)
        ?? throw new InvalidOperationException("پاسخ کاربر معتبر نبود.");

    public async Task<AccountUserResponse> CreateAsync(
        SaveAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PostAsJsonAsync("api/users", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    public async Task<AccountUserResponse> UpdateAsync(
        string userId,
        UpdateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await Client.PutAsJsonAsync($"api/users/{userId}", request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadAsync(response, cancellationToken);
    }

    private static async Task<AccountUserResponse> ReadAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<AccountUserResponse>(cancellationToken)
        ?? throw new InvalidOperationException("پاسخ کاربر معتبر نبود.");

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
