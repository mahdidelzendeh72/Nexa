using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Tests;

public sealed class ModelProviderDomainTests
{
    [Fact]
    public void Register_rejects_a_base_address_with_credentials()
    {
        Assert.Throws<ArgumentException>(() => Create(
            baseAddress: new Uri("https://user:secret@api.openai.com")));
    }

    [Fact]
    public void Missing_max_tokens_defaults_to_4096()
    {
        DiscoveredModel model = new("gpt-4o", null, null, ModelCapability.None);

        Assert.Equal(4096, model.MaxTokens);
    }

    [Fact]
    public void Discovery_stores_reported_capabilities_and_clears_the_previous_error()
    {
        ModelProvider provider = Create();
        provider.RecordDiscoveryFailure("اتصال قطع شد.", DateTimeOffset.UtcNow);

        provider.RecordDiscovery(
            [
                new DiscoveredModel(
                    "llama3.2",
                    DateTimeOffset.Parse("2026-03-01T10:00:00Z"),
                    131072,
                    ModelCapability.LargeLanguage | ModelCapability.ToolCalling),
            ],
            DateTimeOffset.UtcNow);

        ProviderModel model = Assert.Single(provider.Models);
        Assert.Equal("llama3.2", model.Name);
        Assert.Equal(131072, model.MaxTokens);
        Assert.True(model.Capabilities.HasFlag(ModelCapability.ToolCalling));
        Assert.Null(provider.DiscoveryError);
    }

    [Fact]
    public void Changing_the_base_address_clears_previously_read_models()
    {
        ModelProvider provider = Create();
        provider.RecordDiscovery(
            [new DiscoveredModel("gpt-4o", null, null, ModelCapability.LargeLanguage)],
            DateTimeOffset.UtcNow);

        provider.Update(
            provider.Name,
            provider.Kind,
            new Uri("https://example.test/v1"),
            apiKey: null,
            replaceApiKey: false,
            provider.Protocol,
            DateTimeOffset.UtcNow);

        Assert.Empty(provider.Models);
        Assert.True(provider.HasApiKey);
    }

    private static ModelProvider Create(Uri? baseAddress = null) =>
        ModelProvider.Register(
            Guid.NewGuid(),
            "OpenAI",
            ModelProviderKind.OpenAI,
            baseAddress ?? new Uri("https://api.openai.com"),
            "secret",
            ModelProtocol.Responses,
            DateTimeOffset.UtcNow);
}
