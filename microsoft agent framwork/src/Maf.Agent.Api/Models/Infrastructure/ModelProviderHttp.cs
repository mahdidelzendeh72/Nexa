using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Maf.Agent.Api.Models.Application;

namespace Maf.Agent.Api.Models.Infrastructure;

internal static class ModelProviderHttp
{
    public const string ClientName = "ModelProviders";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<T> SendAsync<T>(
        IHttpClientFactory httpClientFactory,
        ModelProviderConnection connection,
        HttpMethod method,
        Uri address,
        object? body,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = new(method, address);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: Json);
        }

        if (!string.IsNullOrEmpty(connection.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);
        }

        HttpClient client = httpClientFactory.CreateClient(ClientName);
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                string payload = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new ModelDiscoveryException(Failure(connection, payload));
            }

            T? result = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
            return result ?? throw new ModelDiscoveryException(
                $"پاسخ مدل‌های «{connection.Name}» قابل خواندن نبود.");
        }
        catch (ModelDiscoveryException)
        {
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ModelDiscoveryException(
                $"خواندن مدل‌های «{connection.Name}» ناموفق بود: {exception.Message}",
                exception);
        }
    }

    private static string Failure(ModelProviderConnection connection, string body)
    {
        string message = $"خواندن مدل‌های «{connection.Name}» ناموفق بود.";
        try
        {
            ProviderErrorResponse? error = JsonSerializer.Deserialize<ProviderErrorResponse>(body, Json);
            if (!string.IsNullOrWhiteSpace(error?.Error))
            {
                message = $"{message} {error.Error}";
            }
        }
        catch (JsonException)
        {
        }

        if (!string.IsNullOrEmpty(connection.ApiKey))
        {
            message = message.Replace(connection.ApiKey, "***", StringComparison.Ordinal);
        }

        return message.Length <= 2000 ? message : message[..2000];
    }
}

internal sealed class ProviderErrorResponse
{
    [JsonConverter(typeof(ProviderErrorConverter))]
    public string? Error { get; init; }
}

internal sealed class ProviderErrorConverter : JsonConverter<string?>
{
    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }

        string? message = null;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Provider error is not an object.");
            }

            string? name = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException("Provider error object ended early.");
            }

            if (string.Equals(name, "message", StringComparison.OrdinalIgnoreCase) &&
                reader.TokenType == JsonTokenType.String)
            {
                message = reader.GetString();
            }
            else
            {
                reader.Skip();
            }
        }

        return message;
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options) =>
        throw new NotSupportedException();
}
