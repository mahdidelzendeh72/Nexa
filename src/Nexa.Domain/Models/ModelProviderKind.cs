namespace Nexa.Domain.Models;

public enum ModelProviderKind
{
    OpenAICompatible = 0,
    OpenAI = 1,
    AzureOpenAI = 2,
    Ollama = 3,
    Anthropic = 4,
    Foundry = 5
}
