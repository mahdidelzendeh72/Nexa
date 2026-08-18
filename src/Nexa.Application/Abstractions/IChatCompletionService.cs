using Nexa.Domain.Models;

namespace Nexa.Application.Abstractions;

public sealed record ProviderConnection(string Endpoint, string ApiKey);

public interface IProviderConnectionResolver
{
    ProviderConnection Resolve(ModelProvider provider);
}

public sealed record ChatCompletionResult(string Text, int? InputTokens, int? OutputTokens, int? TotalTokens, int LatencyMs);

public interface IChatCompletionService
{
    Task<ChatCompletionResult> CompleteAsync(
        string instructions,
        IReadOnlyList<(string Role, string Content)> history,
        ModelProvider provider,
        ModelProfile profile,
        CancellationToken cancellationToken);
}
