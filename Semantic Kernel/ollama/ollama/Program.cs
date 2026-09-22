using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ollama;
using ollama.Configuration;
using ollama.Sessions;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var ollamaSettings = configuration.GetSection("Ollama").Get<OllamaSettings>()
    ?? throw new InvalidOperationException("Ollama settings are missing in appsettings.json.");

var mcpServers = configuration.GetSection("McpServers").Get<List<McpServerSettings>>() ?? [];

Console.WriteLine("Starting MCP + Semantic Kernel chat...");
Console.WriteLine($"Ollama: {ollamaSettings.Endpoint} / {ollamaSettings.ModelId}");
Console.WriteLine($"Configured MCP servers: {mcpServers.Count}");
Console.WriteLine();

await using var mcpManager = new McpServerManager();
await mcpManager.ConnectAsync(mcpServers);

var kernel = AIKernelFactory.Create(ollamaSettings);

var codeFunctionBridge = new CodeFunctionBridge();
var nativeFunctionCount = codeFunctionBridge.RegisterFunctions(kernel);

var mcpBridge = new McpKernelBridge(mcpManager);
var mcpToolCount = mcpBridge.RegisterTools(kernel);

var totalTools = nativeFunctionCount + mcpToolCount;

mcpManager.PrintDiscoveredTools();
codeFunctionBridge.PrintTools(kernel);
Console.WriteLine($"Total tools registered in Semantic Kernel: {totalTools} ({nativeFunctionCount} native, {mcpToolCount} MCP)");

var toolCallingEnabled = ollamaSettings.ToolCallingEnabled && totalTools > 0;
if (totalTools > 0 && !ollamaSettings.ToolCallingEnabled)
{
    Console.WriteLine("Tool calling is disabled in config. Chat works, but tools will not be invoked.");
}
else if (toolCallingEnabled)
{
    Console.WriteLine("Tool calling is enabled. Use a model that supports tools (e.g. llama3.1, qwen2.5).");
}
else if (totalTools == 0)
{
    Console.WriteLine("No tools available. Running in plain chat mode.");
}

var chatOptions = new ChatOptions
{
    ToolCallingEnabled = toolCallingEnabled
};

var services = new ServiceCollection();
services.AddOllamaChatServices(kernel, chatOptions, ollamaSettings);

await using var serviceProvider = services.BuildServiceProvider();
await using var scope = serviceProvider.CreateAsyncScope();
var scopedServices = scope.ServiceProvider;

var chatService = scopedServices.GetRequiredService<ChatService>();
var sessionManager = scopedServices.GetRequiredService<IChatSessionManager>();

sessionManager.EnsureActiveSession();
var initialSession = sessionManager.GetActiveSession();

Console.WriteLine($"User: {sessionManager.CurrentUserId}");
Console.WriteLine($"Active session: {initialSession.SessionId} ({initialSession.Title ?? "New chat"})");
Console.WriteLine("Commands: 'session ...', 'user set <id>', 'model set <id>', 'temp set <0-2>', 'tools ...', 'exit'.");
Console.WriteLine(new string('-', 60));

while (true)
{
    var activeSession = sessionManager.GetActiveSession();
    Console.Write($"\n[{sessionManager.CurrentUserId}/{activeSession.SessionId}] You: ");
    var input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)
        || input.Equals("quit", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    if (TryHandleSessionCommand(input, sessionManager, activeSession, ollamaSettings.ModelId))
    {
        continue;
    }

    if (input.Equals("tools", StringComparison.OrdinalIgnoreCase))
    {
        ToolNameResolver.PrintAllTools(kernel);
        continue;
    }

    if (input.Equals("tools auto", StringComparison.OrdinalIgnoreCase))
    {
        activeSession.SelectedToolNames = null;
        Console.WriteLine("Tool mode: Auto (all tools, optional).");
        continue;
    }

    if (input.Equals("tools selected", StringComparison.OrdinalIgnoreCase))
    {
        if (activeSession.SelectedToolNames is null or { Length: 0 })
        {
            Console.WriteLine("No required tools selected. Mode: Auto.");
        }
        else
        {
            Console.WriteLine("Required tools:");
            foreach (var tool in activeSession.SelectedToolNames)
            {
                Console.WriteLine($"  - {tool}");
            }
        }

        continue;
    }

    if (input.StartsWith("tools set ", StringComparison.OrdinalIgnoreCase))
    {
        activeSession.SelectedToolNames = input["tools set ".Length..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Console.WriteLine("Required tools set for current session:");
        foreach (var tool in activeSession.SelectedToolNames)
        {
            Console.WriteLine($"  - {tool}");
        }

        continue;
    }

    if (input.StartsWith("model set ", StringComparison.OrdinalIgnoreCase))
    {
        var modelId = input["model set ".Length..].Trim();
        activeSession.ModelId = string.IsNullOrWhiteSpace(modelId) ? null : modelId;
        Console.WriteLine($"Session model: {activeSession.ModelId ?? ollamaSettings.ModelId}");
        continue;
    }

    if (input.Equals("model current", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Session model: {activeSession.ModelId ?? "(default)"}");
        Console.WriteLine($"Default model: {ollamaSettings.ModelId}");
        continue;
    }

    if (input.StartsWith("temp set ", StringComparison.OrdinalIgnoreCase))
    {
        var value = input["temp set ".Length..].Trim();
        if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var temperature))
        {
            Console.WriteLine("Usage: temp set <0-2>");
            continue;
        }

        activeSession.Temperature = Math.Clamp(temperature, 0f, 2f);
        Console.WriteLine($"Session temperature: {activeSession.Temperature:0.##}");
        continue;
    }

    if (input.Equals("temp current", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Session temperature: {activeSession.Temperature?.ToString("0.##") ?? "(default)"}");
        Console.WriteLine($"Default temperature: {ollamaSettings.Temperature:0.##}");
        continue;
    }

    try
    {
        Console.Write("\nAssistant: ");

        await foreach (var streamEvent in chatService.ChatStreamAsync(input))
        {
            if (streamEvent.Type == "chunk" && !string.IsNullOrEmpty(streamEvent.Text))
            {
                Console.Write(streamEvent.Text);
            }
        }

        Console.WriteLine();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\nError: {ex.Message}");
    }
}

Console.WriteLine("Goodbye.");

static bool TryHandleSessionCommand(
    string input,
    IChatSessionManager sessionManager,
    ChatSession activeSession,
    string defaultModelId)
{
    if (input.Equals("session current", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine($"User: {sessionManager.CurrentUserId}");
        Console.WriteLine($"Session: {activeSession.SessionId}");
        Console.WriteLine($"Title: {activeSession.Title ?? "(untitled)"}");
        Console.WriteLine($"Model: {activeSession.ModelId ?? defaultModelId}");
        Console.WriteLine($"Temperature: {activeSession.Temperature?.ToString("0.##") ?? "(default)"}");
        Console.WriteLine($"Created: {activeSession.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        return true;
    }

    if (input.Equals("session list", StringComparison.OrdinalIgnoreCase))
    {
        var sessions = sessionManager.ListSessions();
        if (sessions.Count == 0)
        {
            Console.WriteLine("No sessions.");
            return true;
        }

        foreach (var session in sessions)
        {
            var marker = session.IsActive ? "*" : " ";
            var title = string.IsNullOrWhiteSpace(session.Title) ? "(untitled)" : session.Title;
            Console.WriteLine(
                $"{marker} {session.SessionId} | {title} | {session.MessageCount} msgs | {session.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        return true;
    }

    if (input.Equals("session new", StringComparison.OrdinalIgnoreCase)
        || input.StartsWith("session new ", StringComparison.OrdinalIgnoreCase))
    {
        string? title = null;
        if (input.StartsWith("session new ", StringComparison.OrdinalIgnoreCase))
        {
            title = input["session new ".Length..].Trim();
            if (title.Length == 0)
            {
                title = null;
            }
        }

        var session = sessionManager.CreateSession(title);
        Console.WriteLine($"New session created: {session.SessionId} ({session.Title ?? "New chat"})");
        return true;
    }

    if (input.StartsWith("session use ", StringComparison.OrdinalIgnoreCase))
    {
        var sessionId = input["session use ".Length..].Trim();
        if (sessionId.Length == 0)
        {
            Console.WriteLine("Usage: session use <session-id>");
            return true;
        }

        try
        {
            sessionManager.SwitchSession(sessionId);
            var session = sessionManager.GetActiveSession();
            Console.WriteLine($"Switched to session: {session.SessionId} ({session.Title ?? "untitled"})");
        }
        catch (KeyNotFoundException)
        {
            Console.WriteLine($"Session '{sessionId}' was not found for user '{sessionManager.CurrentUserId}'.");
        }

        return true;
    }

    if (input.StartsWith("user set ", StringComparison.OrdinalIgnoreCase))
    {
        var userId = input["user set ".Length..].Trim();
        if (userId.Length == 0)
        {
            Console.WriteLine("Usage: user set <user-id>");
            return true;
        }

        sessionManager.SetUser(userId);
        sessionManager.EnsureActiveSession();
        var session = sessionManager.GetActiveSession();
        Console.WriteLine($"Active user: {sessionManager.CurrentUserId}");
        Console.WriteLine($"Active session: {session.SessionId} ({session.Title ?? "New chat"})");
        return true;
    }

    return false;
}
