using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Maf.Client.Authentication;

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed record TokenResponse(
    [property: JsonPropertyName("tokenType")] string TokenType,
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("expiresIn")] long ExpiresIn,
    [property: JsonPropertyName("refreshToken")] string RefreshToken);

public sealed record StoredTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc);

public sealed record CurrentUser(
    string? UserId,
    string? Email,
    string[] Roles);

public sealed record LoginResult(bool Succeeded, string? Error = null);
