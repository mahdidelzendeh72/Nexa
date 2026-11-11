using Maf.Agent.Api.Models.Application;
using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Tests;

public sealed class ModelDefaultRegistryTests
{
    [Fact]
    public async Task Choices_include_only_models_with_the_operation_capability()
    {
        ModelProvider visionProvider = Provider(
            "Ollama",
            new DiscoveredModel("llava", null, null, ModelCapability.Vision | ModelCapability.LargeLanguage),
            new DiscoveredModel("llama3.2", null, null, ModelCapability.LargeLanguage));
        ModelProvider embeddingProvider = Provider(
            "OpenAI",
            new DiscoveredModel("text-embedding-3-small", null, null, ModelCapability.TextEmbedding));
        ModelDefaultRegistry registry = Registry(visionProvider, embeddingProvider);

        IReadOnlyList<ModelDefaultSelection> rows = await registry.ListAsync(CancellationToken.None);

        ModelDefaultSelection vision = rows.Single(row => row.Operation == ModelOperation.Vision);
        Assert.Null(vision.Selected);
        ModelDefaultChoice onlyVision = Assert.Single(vision.Choices);
        Assert.Equal("llava", onlyVision.ModelName);
        Assert.Equal("text-embedding-3-small", Assert.Single(
            rows.Single(row => row.Operation == ModelOperation.Embedding).Choices).ModelName);
        Assert.Empty(rows.Single(row => row.Operation == ModelOperation.SpeechToText).Choices);
        Assert.Empty(rows.Single(row => row.Operation == ModelOperation.ImageGeneration).Choices);
    }

    [Fact]
    public async Task Save_rejects_a_model_without_the_capability_and_keeps_an_empty_default()
    {
        ModelProvider provider = Provider(
            "OpenAI",
            new DiscoveredModel("gpt-4o", null, null, ModelCapability.LargeLanguage | ModelCapability.Vision),
            new DiscoveredModel("whisper-1", null, null, ModelCapability.AudioInput));
        ModelDefaultRegistry registry = Registry(provider);

        await Assert.ThrowsAsync<ArgumentException>(() => registry.SaveAsync(
            Commands(provider.Id, "gpt-4o", ModelOperation.SpeechToText),
            CancellationToken.None));

        IReadOnlyList<ModelDefaultSelection> saved = await registry.SaveAsync(
            Commands(provider.Id, "whisper-1", ModelOperation.SpeechToText),
            CancellationToken.None);

        Assert.Equal("whisper-1", saved.Single(row => row.Operation == ModelOperation.SpeechToText).Selected?.ModelName);
        Assert.Null(saved.Single(row => row.Operation == ModelOperation.Vision).Selected);

        IReadOnlyList<ModelDefaultSelection> cleared = await registry.SaveAsync(
            Commands(null, null, ModelOperation.SpeechToText),
            CancellationToken.None);
        Assert.Null(cleared.Single(row => row.Operation == ModelOperation.SpeechToText).Selected);
    }

    [Fact]
    public async Task A_default_is_cleared_when_its_model_no_longer_has_the_capability()
    {
        ModelProvider provider = Provider(
            "OpenAI",
            new DiscoveredModel("gpt-4o", null, null, ModelCapability.Vision));
        FakeDefaultStore store = new();
        ModelDefaultRegistry registry = new(new FakeProviderStore(provider), store);
        await registry.SaveAsync(Commands(provider.Id, "gpt-4o", ModelOperation.Vision), CancellationToken.None);

        provider.RecordDiscovery(
            [new DiscoveredModel("gpt-4o", null, null, ModelCapability.LargeLanguage)],
            DateTimeOffset.UtcNow);

        ModelDefaultSelection vision = (await registry.ListAsync(CancellationToken.None))
            .Single(row => row.Operation == ModelOperation.Vision);
        Assert.Null(vision.Selected);
        Assert.Empty(store.Items);
    }

    private static ModelDefaultRegistry Registry(params ModelProvider[] providers) =>
        new(new FakeProviderStore(providers), new FakeDefaultStore());

    private static IReadOnlyList<SaveModelDefault> Commands(
        Guid? providerId,
        string? modelName,
        ModelOperation selected) =>
        Enum.GetValues<ModelOperation>()
            .Select(operation => new SaveModelDefault(
                operation,
                operation == selected ? providerId : null,
                operation == selected ? modelName : null))
            .ToArray();

    private static ModelProvider Provider(string name, params DiscoveredModel[] models)
    {
        ModelProvider provider = ModelProvider.Register(
            Guid.NewGuid(),
            name,
            ModelProviderKind.OpenAI,
            new Uri("https://api.openai.com"),
            null,
            ModelProtocol.Responses,
            DateTimeOffset.UtcNow);
        provider.RecordDiscovery(models, DateTimeOffset.UtcNow);
        return provider;
    }

    private sealed class FakeProviderStore(params ModelProvider[] providers) : IModelProviderStore
    {
        public Task<IReadOnlyList<ModelProvider>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ModelProvider>>(providers);

        public Task<ModelProvider?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(providers.SingleOrDefault(provider => provider.Id == id));

        public Task AddAsync(ModelProvider provider, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteAsync(ModelProvider provider, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ThrowIfNameTakenAsync(
            Guid providerId,
            string normalizedName,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeDefaultStore : IModelDefaultStore
    {
        public List<ModelDefault> Items { get; } = [];

        public Task<IReadOnlyList<ModelDefault>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ModelDefault>>(Items.ToArray());

        public void Add(ModelDefault modelDefault) => Items.Add(modelDefault);

        public void Remove(ModelDefault modelDefault) => Items.Remove(modelDefault);

        public Task ReleaseIncompatibleAsync(ModelProvider provider, CancellationToken cancellationToken)
        {
            Items.RemoveAll(item =>
                item.ProviderId == provider.Id &&
                !item.Accepts(provider.Models.FirstOrDefault(model => model.Name == item.ModelName)));
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
