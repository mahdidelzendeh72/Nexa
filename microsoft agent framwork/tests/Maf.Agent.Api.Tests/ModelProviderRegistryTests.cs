using Maf.Agent.Api.Models.Application;
using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Tests;

public sealed class ModelProviderRegistryTests
{
    [Fact]
    public async Task Register_stores_the_provider_and_the_models_it_returns()
    {
        FakeStore store = new();
        ModelProviderRegistry registry = new(
            store,
            [
                new FakeCatalog(
                [
                    new DiscoveredModel("gpt-4o", null, null, ModelCapability.LargeLanguage),
                ]),
            ],
            new FakeDefaults());

        ModelProvider provider = await registry.RegisterAsync(Command(), CancellationToken.None);

        Assert.Equal("gpt-4o", Assert.Single(provider.Models).Name);
        Assert.Equal(4096, Assert.Single(provider.Models).MaxTokens);
        Assert.Null(provider.DiscoveryError);
        Assert.Single(store.Providers);
    }

    [Fact]
    public async Task Register_keeps_the_provider_when_reading_models_fails()
    {
        FakeStore store = new();
        ModelProviderRegistry registry = new(store, [new ThrowingCatalog()], new FakeDefaults());

        ModelProvider provider = await registry.RegisterAsync(Command(), CancellationToken.None);

        Assert.Empty(provider.Models);
        Assert.Contains("ناموفق", provider.DiscoveryError, StringComparison.Ordinal);
        Assert.Single(store.Providers);
    }

    private static SaveModelProvider Command() =>
        new(
            "OpenAI",
            ModelProviderKind.OpenAI,
            "https://api.openai.com",
            "secret",
            true,
            ModelProtocol.Responses);

    private sealed class FakeStore : IModelProviderStore
    {
        public List<ModelProvider> Providers { get; } = [];

        public Task<IReadOnlyList<ModelProvider>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ModelProvider>>(Providers);

        public Task<ModelProvider?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Providers.SingleOrDefault(provider => provider.Id == id));

        public Task AddAsync(ModelProvider provider, CancellationToken cancellationToken)
        {
            Providers.Add(provider);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(ModelProvider provider, CancellationToken cancellationToken)
        {
            Providers.Remove(provider);
            return Task.CompletedTask;
        }

        public Task ThrowIfNameTakenAsync(
            Guid providerId,
            string normalizedName,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeDefaults : IModelDefaultStore
    {
        public Task<IReadOnlyList<ModelDefault>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ModelDefault>>([]);

        public void Add(ModelDefault modelDefault)
        {
        }

        public void Remove(ModelDefault modelDefault)
        {
        }

        public Task ReleaseIncompatibleAsync(ModelProvider provider, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeCatalog(IReadOnlyList<DiscoveredModel> models) : IModelCatalog
    {
        public bool Supports(ModelProviderKind kind) => true;

        public Task<IReadOnlyList<DiscoveredModel>> ListAsync(
            ModelProviderConnection connection,
            CancellationToken cancellationToken) =>
            Task.FromResult(models);
    }

    private sealed class ThrowingCatalog : IModelCatalog
    {
        public bool Supports(ModelProviderKind kind) => true;

        public Task<IReadOnlyList<DiscoveredModel>> ListAsync(
            ModelProviderConnection connection,
            CancellationToken cancellationToken) =>
            throw new ModelDiscoveryException("خواندن مدل‌ها ناموفق بود.");
    }
}
