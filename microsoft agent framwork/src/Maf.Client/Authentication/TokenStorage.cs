using System.Text.Json;
using Microsoft.JSInterop;

namespace Maf.Client.Authentication;

public sealed class TokenStorage(IJSRuntime jsRuntime)
{
    private const string StorageKey = "maf.auth.tokens";

    public async ValueTask<StoredTokens?> GetAsync()
    {
        string? json = await jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            StorageKey);

        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<StoredTokens>(json);
    }

    public ValueTask SetAsync(StoredTokens tokens) =>
        jsRuntime.InvokeVoidAsync(
            "localStorage.setItem",
            StorageKey,
            JsonSerializer.Serialize(tokens));

    public ValueTask ClearAsync() =>
        jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
}
