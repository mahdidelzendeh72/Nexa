using System.Net.Http.Headers;

namespace Maf.Client.Authentication;

public sealed class BearerTokenHandler(TokenStorage tokenStorage) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        StoredTokens? tokens = await tokenStorage.GetAsync();
        if (tokens is not null && tokens.ExpiresAtUtc > DateTimeOffset.UtcNow)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
