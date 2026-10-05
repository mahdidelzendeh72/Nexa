using Microsoft.SemanticKernel;
using ollama.Configuration;

namespace ollama;

public sealed class McpKernelBridge
{
    private readonly McpServerManager _mcpManager;

    public McpKernelBridge(McpServerManager mcpManager)
    {
        _mcpManager = mcpManager;
    }

    public int RegisterTools(Kernel kernel) =>
        _mcpManager.RegisterTools(kernel);

    public IReadOnlyList<DiscoveredTool> GetDiscoveredTools() =>
        _mcpManager.DiscoveredTools;
}
