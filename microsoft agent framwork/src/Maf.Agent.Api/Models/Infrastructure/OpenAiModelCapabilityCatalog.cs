using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Models.Infrastructure;

public static class OpenAiModelCapabilityCatalog
{
    public static ModelCapability Get(string? modelId)
    {
        string id = (modelId ?? string.Empty).ToLowerInvariant();
        if (id.Contains("text-embedding", StringComparison.Ordinal) ||
            id.Contains("embed", StringComparison.Ordinal))
        {
            return ModelCapability.TextEmbedding;
        }

        if (id.Contains("whisper", StringComparison.Ordinal))
        {
            return ModelCapability.AudioInput;
        }

        if (id.Contains("tts", StringComparison.Ordinal))
        {
            return ModelCapability.AudioOutput;
        }

        if (id.Contains("dall-e", StringComparison.Ordinal) ||
            id.Contains("gpt-image", StringComparison.Ordinal))
        {
            return ModelCapability.ImageGeneration;
        }

        if (id.StartsWith("gpt-4o", StringComparison.Ordinal) ||
            id.StartsWith("gpt-4.1", StringComparison.Ordinal))
        {
            return ModelCapability.LargeLanguage | ModelCapability.Vision | ModelCapability.ToolCalling;
        }

        if (id.StartsWith("o1", StringComparison.Ordinal) ||
            id.StartsWith("o3", StringComparison.Ordinal) ||
            id.StartsWith("o4", StringComparison.Ordinal))
        {
            return ModelCapability.LargeLanguage | ModelCapability.Reasoning | ModelCapability.ToolCalling;
        }

        return ModelCapability.None;
    }

    public static int? ContextLength(string? modelId)
    {
        string id = (modelId ?? string.Empty).ToLowerInvariant();
        if (id.StartsWith("gpt-4.1", StringComparison.Ordinal))
        {
            return 1_048_576;
        }

        if (id.StartsWith("gpt-4o", StringComparison.Ordinal))
        {
            return 128_000;
        }

        return null;
    }
}
