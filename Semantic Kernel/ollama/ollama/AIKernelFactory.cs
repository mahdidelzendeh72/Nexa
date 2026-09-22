using Microsoft.SemanticKernel;
using ollama.Configuration;
using ollama.Middleware;

namespace ollama;

public static class AIKernelFactory
{
    public static Kernel Create(OllamaSettings settings)
    {
        var builder = Kernel.CreateBuilder();

        builder.AddOllamaChatCompletion(
            modelId: settings.ModelId,
            endpoint: new Uri(settings.Endpoint));

        var kernel = builder.Build();
        kernel.FunctionInvocationFilters.Add(new ToolInvocationLoggingFilter());
        kernel.PromptRenderFilters.Add(new PromptSafetyFilter());

        return kernel;
    }
}
