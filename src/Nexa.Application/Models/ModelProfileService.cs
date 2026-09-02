using FluentValidation;
using Nexa.Application.Abstractions;
using Nexa.Application.Agents;
using Nexa.Application.Common;
using Nexa.Contracts.Models;
using Nexa.Domain.Models;

namespace Nexa.Application.Models;

public interface IModelProfileService
{
    Task<IReadOnlyList<ModelProviderDto>> ListProvidersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ModelProfileDto>> ListProfilesAsync(CancellationToken cancellationToken);
    Task<ModelProfileDto> GetProfileAsync(Guid id, CancellationToken cancellationToken);
    Task<ModelProfileDto> CreateProfileAsync(CreateModelProfileRequest request, CancellationToken cancellationToken);
    Task<ModelProfileDto> UpdateProfileAsync(Guid id, UpdateModelProfileRequest request, CancellationToken cancellationToken);
}

public sealed class ModelProfileService(
    IModelCatalogRepository catalog,
    IUnitOfWork unitOfWork,
    IValidator<CreateModelProfileRequest> createValidator,
    IValidator<UpdateModelProfileRequest> updateValidator) : IModelProfileService
{
    public async Task<IReadOnlyList<ModelProviderDto>> ListProvidersAsync(CancellationToken cancellationToken)
    {
        var providers = await catalog.ListProvidersAsync(cancellationToken);
        return providers.Select(p => new ModelProviderDto(p.Id, p.Name, p.Kind.ToString(), p.IsEnabled)).ToList();
    }

    public async Task<IReadOnlyList<ModelProfileDto>> ListProfilesAsync(CancellationToken cancellationToken)
    {
        var providers = (await catalog.ListProvidersAsync(cancellationToken)).ToDictionary(p => p.Id);
        var profiles = await catalog.ListProfilesAsync(cancellationToken);
        return profiles.Select(p => ToDto(p, providers)).ToList();
    }

    public async Task<ModelProfileDto> GetProfileAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await catalog.GetProfileAsync(id, cancellationToken)
            ?? throw NexaException.NotFound("Model profile was not found.");
        var provider = await catalog.GetProviderAsync(profile.ProviderId, cancellationToken)
            ?? throw NexaException.NotFound("Model provider was not found.");
        return ToDto(profile, new Dictionary<Guid, ModelProvider> { [provider.Id] = provider });
    }

    public async Task<ModelProfileDto> CreateProfileAsync(CreateModelProfileRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);
        var provider = await catalog.GetProviderAsync(request.ProviderId, cancellationToken)
            ?? throw NexaException.NotFound("Model provider was not found.");

        var profile = ModelProfile.Create(
            provider.Id,
            request.Name,
            request.ModelId,
            request.Temperature,
            request.MaxTokens,
            request.IsEnabled);

        await catalog.AddProfileAsync(profile, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(profile, new Dictionary<Guid, ModelProvider> { [provider.Id] = provider });
    }

    public async Task<ModelProfileDto> UpdateProfileAsync(Guid id, UpdateModelProfileRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken);
        var profile = await catalog.GetProfileAsync(id, cancellationToken)
            ?? throw NexaException.NotFound("Model profile was not found.");
        var provider = await catalog.GetProviderAsync(profile.ProviderId, cancellationToken)
            ?? throw NexaException.NotFound("Model provider was not found.");

        profile.Update(request.Name, request.ModelId, request.Temperature, request.MaxTokens, request.IsEnabled);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(profile, new Dictionary<Guid, ModelProvider> { [provider.Id] = provider });
    }

    private static ModelProfileDto ToDto(ModelProfile profile, IReadOnlyDictionary<Guid, ModelProvider> providers)
    {
        var providerName = providers.TryGetValue(profile.ProviderId, out var provider) ? provider.Name : "Unknown";
        return new ModelProfileDto(
            profile.Id,
            profile.ProviderId,
            providerName,
            profile.Name,
            profile.ModelId,
            profile.Temperature,
            profile.MaxTokens,
            profile.IsEnabled);
    }
}
