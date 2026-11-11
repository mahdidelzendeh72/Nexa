using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Models.Infrastructure;

internal static class ModelCapabilities
{
    public static ModelCapability From(IEnumerable<string>? names)
    {
        ModelCapability capabilities = ModelCapability.None;
        if (names is null)
        {
            return capabilities;
        }

        foreach (string name in names)
        {
            capabilities |= Map(name);
        }

        return capabilities;
    }

    private static ModelCapability Map(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "language" or "completion" or "chat" => ModelCapability.LargeLanguage,
            "vision" or "image_input" or "input_image" => ModelCapability.Vision,
            "image" or "image_generation" or "image_output" => ModelCapability.ImageGeneration,
            "tools" or "tool_calling" or "function_calling" => ModelCapability.ToolCalling,
            "reasoning" or "thinking" => ModelCapability.Reasoning,
            "audio_input" or "input_audio" => ModelCapability.AudioInput,
            "audio_output" or "output_audio" => ModelCapability.AudioOutput,
            "audio" => ModelCapability.AudioInput | ModelCapability.AudioOutput,
            "embedding" or "text_embedding" => ModelCapability.TextEmbedding,
            _ => ModelCapability.None,
        };
}
