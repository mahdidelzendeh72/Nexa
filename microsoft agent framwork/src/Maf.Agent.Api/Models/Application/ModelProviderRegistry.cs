using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Models.Application;

public sealed class ModelProviderRegistry(
    IModelProviderStore store,
    IEnumerable<IModelCatalog> catalogs,
    IModelDefaultStore defaults)
{
    public Task<IReadOnlyList<ModelProvider>> ListAsync(CancellationToken cancellationToken) =>
        store.ListAsync(cancellationToken);

    public Task<ModelProvider?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        store.FindAsync(id, cancellationToken);

    public async Task<ModelProvider> RegisterAsync(
        SaveModelProvider command,
        CancellationToken cancellationToken)
    {
        ModelProvider provider = ModelProvider.Register(
            Guid.NewGuid(),
            command.Name,
            command.Kind,
            ParseBaseAddress(command.BaseAddress),
            command.ApiKey,
            command.Protocol,
            DateTimeOffset.UtcNow);
        await store.ThrowIfNameTakenAsync(
            provider.Id,
            provider.NormalizedName,
            cancellationToken);
        await store.AddAsync(provider, cancellationToken);
        await DiscoverCoreAsync(provider, cancellationToken);
        return provider;
    }

    public async Task<ModelProvider?> UpdateAsync(
        Guid id,
        SaveModelProvider command,
        CancellationToken cancellationToken)
    {
        ModelProvider? provider = await store.FindAsync(id, cancellationToken);
        if (provider is null)
        {
            return null;
        }

        provider.Update(
            command.Name,
            command.Kind,
            ParseBaseAddress(command.BaseAddress),
            command.ApiKey,
            command.ReplaceApiKey,
            command.Protocol,
            DateTimeOffset.UtcNow);
        await store.ThrowIfNameTakenAsync(
            provider.Id,
            provider.NormalizedName,
            cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        await DiscoverCoreAsync(provider, cancellationToken);
        return provider;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        ModelProvider? provider = await store.FindAsync(id, cancellationToken);
        if (provider is null)
        {
            return false;
        }

        await store.DeleteAsync(provider, cancellationToken);
        return true;
    }

    public async Task<ModelProvider?> DiscoverAsync(Guid id, CancellationToken cancellationToken)
    {
        ModelProvider? provider = await store.FindAsync(id, cancellationToken);
        if (provider is null)
        {
            return null;
        }

        await DiscoverCoreAsync(provider, cancellationToken);
        return provider;
    }

    private async Task DiscoverCoreAsync(
        ModelProvider provider,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<DiscoveredModel> models = await CatalogFor(provider.Kind).ListAsync(
                new ModelProviderConnection(
                    provider.Id,
                    provider.Name,
                    provider.Kind,
                    provider.BaseAddress,
                    provider.ApiKey,
                    provider.Protocol),
                cancellationToken);
            provider.RecordDiscovery(models, DateTimeOffset.UtcNow);
        }
        catch (ModelDiscoveryException exception)
        {
            provider.RecordDiscoveryFailure(exception.Message, DateTimeOffset.UtcNow);
        }
        catch (ArgumentException exception)
        {
            provider.RecordDiscoveryFailure(exception.Message, DateTimeOffset.UtcNow);
        }

        await defaults.ReleaseIncompatibleAsync(provider, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    private IModelCatalog CatalogFor(ModelProviderKind kind) =>
        catalogs.FirstOrDefault(catalog => catalog.Supports(kind))
        ?? throw new ModelDiscoveryException("برای این نوع ارائه‌دهنده کاتالوگی ثبت نشده است.");

    private static Uri ParseBaseAddress(string baseAddress)
    {
        if (string.IsNullOrWhiteSpace(baseAddress) ||
            !Uri.TryCreate(baseAddress.Trim(), UriKind.Absolute, out Uri? uri))
        {
            throw new ArgumentException("نشانی پایه باید یک URL مطلق http یا https باشد.");
        }

        return uri;
    }
}
