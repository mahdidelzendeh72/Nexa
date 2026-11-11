namespace Maf.Client.Models;

public sealed record ModelProviderSummary(
    Guid Id,
    string Name,
    string Kind,
    string BaseAddress,
    bool HasApiKey,
    string Protocol,
    DateTimeOffset? LastDiscoveredAt,
    string? DiscoveryError,
    int ModelCount);

public sealed record ModelProviderDetails(
    Guid Id,
    string Name,
    string Kind,
    string BaseAddress,
    bool HasApiKey,
    string Protocol,
    DateTimeOffset? LastDiscoveredAt,
    string? DiscoveryError,
    ProviderModelDetails[] Models);

public sealed record ProviderModelDetails(
    string Name,
    DateTimeOffset? LastChangedAt,
    int MaxTokens,
    string[] Capabilities);

public sealed record SaveModelProviderRequest(
    string Name,
    string Kind,
    string BaseAddress,
    string? ApiKey,
    string Protocol);

public sealed record ModelDefaultsResponse(ModelDefaultResponse[] Operations);

public sealed record ModelDefaultResponse(
    string Operation,
    Guid? ProviderId,
    string? ProviderName,
    string? ModelName,
    ModelDefaultChoiceResponse[] Choices);

public sealed record ModelDefaultChoiceResponse(Guid ProviderId, string ProviderName, string ModelName);

public sealed record SaveModelDefaultsRequest(SaveModelDefaultRequest[] Operations);

public sealed record SaveModelDefaultRequest(string Operation, Guid? ProviderId, string? ModelName);
