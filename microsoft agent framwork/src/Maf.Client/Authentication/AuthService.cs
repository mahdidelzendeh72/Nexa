using System.Net.Http.Json;
using System.Text.Json;

namespace Maf.Client.Authentication;

public sealed class AuthService(
    IHttpClientFactory httpClientFactory,
    TokenStorage tokenStorage,
    TokenAuthenticationStateProvider authenticationStateProvider)
{
    public async Task<LoginResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        HttpClient client = httpClientFactory.CreateClient("Backend");
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            "identity/login",
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return new LoginResult(
                false,
                "ایمیل یا رمز عبور صحیح نیست.");
        }

        TokenResponse? tokenResponse =
            await response.Content.ReadFromJsonAsync<TokenResponse>(
                cancellationToken: cancellationToken);

        if (tokenResponse is null ||
            string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            return new LoginResult(
                false,
                "پاسخ سرویس ورود معتبر نبود.");
        }

        StoredTokens tokens = new(
            tokenResponse.AccessToken,
            tokenResponse.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn));

        await tokenStorage.SetAsync(tokens);
        authenticationStateProvider.Refresh();

        return new LoginResult(true);
    }

    public async Task LogoutAsync()
    {
        await tokenStorage.ClearAsync();
        authenticationStateProvider.Refresh();
    }
}
