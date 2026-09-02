namespace Nexa.Contracts.Models;

public sealed record ModelProviderDto(Guid Id, string Name, string Kind, bool IsEnabled);

public sealed record ModelProfileDto(
    Guid Id,
    Guid ProviderId,
    string ProviderName,
    string Name,
    string ModelId,
    double? Temperature,
    int? MaxTokens,
    bool IsEnabled);

public sealed record CreateModelProfileRequest(
    Guid ProviderId,
    string Name,
    string ModelId,
    double? Temperature,
    int? MaxTokens,
    bool IsEnabled);

public sealed record UpdateModelProfileRequest(
    string Name,
    string ModelId,
    double? Temperature,
    int? MaxTokens,
    bool IsEnabled);
