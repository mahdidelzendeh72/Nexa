using Maf.Agent.Api.Models.Application;
using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Chat.Application;

public sealed record ChatModelChoice(Guid ProviderId, string ProviderName, string ModelName);

public sealed record ChatConnection(
    ModelProviderKind Kind,
    ModelProtocol Protocol,
    string ModelName,
    Uri BaseAddress,
    string? ApiKey);

public sealed class ChatModelCatalog(IServiceScopeFactory scopes)
{
    public async Task<IReadOnlyList<ChatModelChoice>> ListAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IModelProviderStore providers = scope.ServiceProvider.GetRequiredService<IModelProviderStore>();
        IReadOnlyList<ModelProvider> stored = await providers.ListAsync(cancellationToken);
        return stored
            .OrderBy(provider => provider.Name, StringComparer.Ordinal)
            .SelectMany(provider => provider.Models
                .OrderBy(model => model.Name, StringComparer.Ordinal)
                .Select(model => new ChatModelChoice(provider.Id, provider.Name, model.Name)))
            .ToArray();
    }

    public async Task<ChatConnection> RequireAsync(
        Guid providerId,
        string modelName,
        CancellationToken cancellationToken)
    {
        if (providerId == Guid.Empty || string.IsNullOrWhiteSpace(modelName))
        {
            throw new InvalidOperationException("یک مدل انتخاب کنید.");
        }

        await using AsyncServiceScope scope = scopes.CreateAsyncScope();
        IModelProviderStore providers = scope.ServiceProvider.GetRequiredService<IModelProviderStore>();
        ModelProvider? provider = await providers.FindAsync(providerId, cancellationToken);
        ProviderModel? model = provider?.Models.FirstOrDefault(
            item => string.Equals(item.Name, modelName, StringComparison.Ordinal));
        if (provider is null || model is null)
        {
            throw new InvalidOperationException("مدل انتخاب‌شده در ارائه‌دهنده‌ها پیدا نشد.");
        }

        return new ChatConnection(
            provider.Kind,
            provider.Protocol,
            model.Name,
            provider.BaseAddress,
            provider.ApiKey);
    }
}
