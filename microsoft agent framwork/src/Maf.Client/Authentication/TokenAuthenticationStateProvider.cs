using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Maf.Client.Authentication;

public sealed class TokenAuthenticationStateProvider(
    IHttpClientFactory httpClientFactory,
    TokenStorage tokenStorage)
    : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        StoredTokens? tokens = await tokenStorage.GetAsync();
        if (tokens is null || tokens.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            await tokenStorage.ClearAsync();
            return Anonymous;
        }

        HttpClient client = httpClientFactory.CreateClient("Backend");
        using HttpResponseMessage response = await client.GetAsync("api/auth/me");

        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            await tokenStorage.ClearAsync();
            return Anonymous;
        }

        if (!response.IsSuccessStatusCode)
        {
            return Anonymous;
        }

        CurrentUser? currentUser =
            await response.Content.ReadFromJsonAsync<CurrentUser>();
        if (string.IsNullOrWhiteSpace(currentUser?.Email))
        {
            return Anonymous;
        }

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, currentUser.UserId ?? currentUser.Email),
            new(ClaimTypes.Name, currentUser.Email),
            new(ClaimTypes.Email, currentUser.Email),
        ];
        claims.AddRange(
            currentUser.Roles.Select(role => new Claim(ClaimTypes.Role, role)));

        ClaimsIdentity identity = new(claims, "IdentityBearer");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void Refresh()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }
}
