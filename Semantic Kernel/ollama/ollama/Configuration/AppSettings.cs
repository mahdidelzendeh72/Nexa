namespace ollama.Configuration;

public sealed class OllamaSettings
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string ModelId { get; set; } = "llama3.1";
    public bool ToolCallingEnabled { get; set; } = true;
    public float Temperature { get; set; } = 0.7f;
}

public sealed class McpServerSettings
{
    public string Name { get; set; } = string.Empty;
    public string Transport { get; set; } = "Http";
    public string TransportMode { get; set; } = "StreamableHttp";
    public string? Endpoint { get; set; }
    public string? Command { get; set; }
    public string[] Arguments { get; set; } = [];
}

public sealed record DiscoveredTool(string ServerName, string ToolName, string? Description);
