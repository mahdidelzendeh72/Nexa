using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using ollama.Configuration;

namespace ollama;

public sealed class OllamaBootstrap(IConfiguration configuration) : IAsyncDisposable
{
    private McpServerManager? _mcpManager;
    private bool _initialized;

    public Kernel Kernel { get; private set; } = null!;

    public ChatOptions ChatOptions { get; private set; } = null!;

    public OllamaSettings OllamaSettings { get; private set; } = null!;

    public int TotalTools { get; private set; }

    public McpServerManager McpManager => _mcpManager
        ?? throw new InvalidOperationException("Ollama infrastructure has not been initialized.");

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        var ollamaSettings = configuration.GetSection("Ollama").Get<OllamaSettings>()
            ?? throw new InvalidOperationException("Ollama settings are missing in configuration.");

        OllamaSettings = ollamaSettings;

        var mcpServers = configuration.GetSection("McpServers").Get<List<McpServerSettings>>() ?? [];

        _mcpManager = new McpServerManager();
        await _mcpManager.ConnectAsync(mcpServers, cancellationToken);

        Kernel = AIKernelFactory.Create(ollamaSettings);

        var codeFunctionBridge = new CodeFunctionBridge();
        var nativeFunctionCount = codeFunctionBridge.RegisterFunctions(Kernel);

        var mcpBridge = new McpKernelBridge(_mcpManager);
        var mcpToolCount = mcpBridge.RegisterTools(Kernel);

        TotalTools = nativeFunctionCount + mcpToolCount;

        var toolCallingEnabled = ollamaSettings.ToolCallingEnabled && TotalTools > 0;
        ChatOptions = new ChatOptions
        {
            ToolCallingEnabled = toolCallingEnabled
        };

        _initialized = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_mcpManager is not null)
        {
            await _mcpManager.DisposeAsync();
            _mcpManager = null;
        }
    }
}
