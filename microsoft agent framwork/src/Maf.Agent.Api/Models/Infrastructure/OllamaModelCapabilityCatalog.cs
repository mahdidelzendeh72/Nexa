using Maf.Agent.Api.Models.Domain;

namespace Maf.Agent.Api.Models.Infrastructure;

public static class OllamaModelCapabilityCatalog
{
    public static ModelCapability Get(OllamaModelResponse model)
    {
        string id = (model.Model ?? model.Name ?? string.Empty).ToLowerInvariant();
        if (id.Contains("embed", StringComparison.Ordinal))
        {
            return ModelCapability.TextEmbedding;
        }

        ModelCapability capabilities = ModelCapability.None;
        IReadOnlyList<string> families = model.Details?.Families ?? [];
        if (families.Any(family =>
                !string.Equals(family, "clip", StringComparison.OrdinalIgnoreCase)))
        {
            capabilities |= ModelCapability.LargeLanguage;
        }

        if (families.Any(family =>
                string.Equals(family, "clip", StringComparison.OrdinalIgnoreCase)))
        {
            capabilities |= ModelCapability.Vision;
        }

        if (id.Contains("deepseek-r1", StringComparison.Ordinal) ||
            id.Contains("qwq", StringComparison.Ordinal))
        {
            capabilities |= ModelCapability.Reasoning;
        }

        return capabilities;
    }
}
