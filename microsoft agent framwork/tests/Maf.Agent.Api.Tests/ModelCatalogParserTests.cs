using Maf.Agent.Api.Models.Domain;
using Maf.Agent.Api.Models.Infrastructure;

namespace Maf.Agent.Api.Tests;

public sealed class ModelCatalogParserTests
{
    [Fact]
    public void OpenAi_list_uses_4096_when_the_payload_has_no_token_limit()
    {
        IReadOnlyList<DiscoveredModel> models = OpenAiModelCatalog.Read(
            """
            {
              "data": [
                { "id": "custom-model", "created": 1715367049 },
                {
                  "id": "vision-model",
                  "created": 1715367049,
                  "max_completion_tokens": 16384,
                  "context_length": 32768,
                  "capabilities": ["vision", "tools", "reasoning"]
                }
              ]
            }
            """);

        Assert.Equal(2, models.Count);
        Assert.Equal(4096, models[0].MaxTokens);
        Assert.Equal(ModelCapability.None, models[0].Capabilities);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1715367049), models[0].LastChangedAt);
        Assert.Equal(32768, models[1].MaxTokens);
        Assert.True(models[1].Capabilities.HasFlag(ModelCapability.Vision));
        Assert.True(models[1].Capabilities.HasFlag(ModelCapability.ToolCalling));
        Assert.True(models[1].Capabilities.HasFlag(ModelCapability.Reasoning));
    }

    [Fact]
    public void Ollama_show_reads_context_length_and_capabilities()
    {
        DiscoveredModel listed = Assert.Single(OllamaModelCatalog.ReadTags(
            """
            {
              "models": [
                { "name": "llama3.2:latest", "modified_at": "2026-03-01T10:00:00Z" }
              ]
            }
            """));

        DiscoveredModel shown = OllamaModelCatalog.ReadShow(
            listed,
            """
            {
              "modified_at": "2026-04-02T08:30:00Z",
              "capabilities": ["completion", "vision", "tools", "thinking"],
              "model_info": { "llama.context_length": 131072 }
            }
            """);

        Assert.Equal("llama3.2:latest", shown.Name);
        Assert.Equal(131072, shown.MaxTokens);
        Assert.Equal(DateTimeOffset.Parse("2026-04-02T08:30:00Z"), shown.LastChangedAt);
        Assert.True(shown.Capabilities.HasFlag(ModelCapability.LargeLanguage));
        Assert.True(shown.Capabilities.HasFlag(ModelCapability.Vision));
        Assert.True(shown.Capabilities.HasFlag(ModelCapability.ToolCalling));
        Assert.True(shown.Capabilities.HasFlag(ModelCapability.Reasoning));
    }

    [Fact]
    public void OpenAi_models_uri_appends_v1_once()
    {
        Assert.Equal(
            "https://api.openai.com/v1/models",
            OpenAiModelCatalog.ModelsUri(new Uri("https://api.openai.com")).AbsoluteUri);
        Assert.Equal(
            "https://gateway.example/v1/models",
            OpenAiModelCatalog.ModelsUri(new Uri("https://gateway.example/v1")).AbsoluteUri);
        Assert.Equal(
            "http://localhost:11434/api/tags",
            new Uri(OllamaModelCatalog.Root(new Uri("http://localhost:11434/v1")), "api/tags").AbsoluteUri);
    }

    [Fact]
    public void OpenAi_known_ids_fill_capabilities_and_token_limits()
    {
        IReadOnlyList<DiscoveredModel> models = OpenAiModelCatalog.Read(
            """
            {
              "data": [
                { "id": "gpt-4o-mini" },
                { "id": "gpt-4.1" },
                { "id": "o3-mini" },
                { "id": "text-embedding-3-small" },
                { "id": "whisper-1" },
                { "id": "tts-1" },
                { "id": "dall-e-3" }
              ]
            }
            """);

        Assert.Equal(128_000, models[0].MaxTokens);
        Assert.Equal(
            ModelCapability.LargeLanguage | ModelCapability.Vision | ModelCapability.ToolCalling,
            models[0].Capabilities);
        Assert.Equal(1_048_576, models[1].MaxTokens);
        Assert.Equal(
            ModelCapability.LargeLanguage | ModelCapability.Reasoning | ModelCapability.ToolCalling,
            models[2].Capabilities);
        Assert.Equal(ModelCapability.TextEmbedding, models[3].Capabilities);
        Assert.Equal(ModelCapability.AudioInput, models[4].Capabilities);
        Assert.Equal(ModelCapability.AudioOutput, models[5].Capabilities);
        Assert.Equal(ModelCapability.ImageGeneration, models[6].Capabilities);
    }

    [Fact]
    public void Ollama_families_mark_language_vision_and_reasoning()
    {
        IReadOnlyList<DiscoveredModel> models = OllamaModelCatalog.ReadTags(
            """
            {
              "models": [
                {
                  "name": "llava:latest",
                  "details": { "families": ["llama", "clip"] }
                },
                { "name": "deepseek-r1:latest", "details": { "families": ["qwen2"] } },
                { "name": "nomic-embed-text:latest", "details": { "families": ["nomic-bert"] } }
              ]
            }
            """);

        Assert.Equal(ModelCapability.LargeLanguage | ModelCapability.Vision, models[0].Capabilities);
        Assert.Equal(
            ModelCapability.LargeLanguage | ModelCapability.Reasoning,
            models[1].Capabilities);
        Assert.Equal(ModelCapability.TextEmbedding, models[2].Capabilities);
    }

    [Fact]
    public void Ollama_modified_at_with_a_local_offset_is_stored_as_utc()
    {
        DiscoveredModel model = Assert.Single(OllamaModelCatalog.ReadTags(
            """
            {
              "models": [
                { "name": "llama3.2:latest", "modified_at": "2026-03-01T10:00:00+03:30" }
              ]
            }
            """));

        Assert.Equal(TimeSpan.Zero, model.LastChangedAt?.Offset);
        Assert.Equal(DateTimeOffset.Parse("2026-03-01T06:30:00Z"), model.LastChangedAt);
    }
}
