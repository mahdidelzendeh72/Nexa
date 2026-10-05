using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;
using ollama.Configuration;
using ollama.Middleware;
using ollama.Sessions;

namespace ollama;

public sealed class ChatService(
    Kernel kernel,
    IChatSessionManager sessionManager,
    ISessionAttachmentService attachmentService,
    ISystemPromptBuilder systemPromptBuilder,
    ChatOptions chatOptions,
    OllamaSettings ollamaSettings,
    IEnumerable<IChatMiddleware> middlewares)
{
    private readonly ChatMiddlewarePipeline _middlewarePipeline = new(middlewares);

    public async Task<ChatTurnResult> ChatAsync(
        string message,
        IEnumerable<string>? toolNames = null,
        string? modelId = null,
        float? temperature = null,
        IEnumerable<string>? fileIds = null,
        CancellationToken cancellationToken = default)
    {
        var prepared = PrepareTurn(message, toolNames, modelId, temperature, fileIds);

        var context = new ChatMiddlewareContext
        {
            UserMessage = prepared.ComposedMessage,
            ToolNames = prepared.EffectiveToolNames,
            History = prepared.Session.History,
            BuildSystemPrompt = systemPromptBuilder.Build
        };

        var response = await _middlewarePipeline.ExecuteAsync(
            context,
            () => ExecuteChatAsync(prepared, cancellationToken));

        return new ChatTurnResult(response, prepared.EffectiveModelId, prepared.EffectiveTemperature);
    }

    public async IAsyncEnumerable<ChatStreamEvent> ChatStreamAsync(
        string message,
        IEnumerable<string>? toolNames = null,
        string? modelId = null,
        float? temperature = null,
        IEnumerable<string>? fileIds = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var prepared = PrepareTurn(message, toolNames, modelId, temperature, fileIds);

        Console.WriteLine($"[Middleware] Stream request: {prepared.ComposedMessage}");
        Console.WriteLine($"[Middleware] Model: {prepared.EffectiveModelId}, Temperature: {prepared.EffectiveTemperature:0.##}");

        RefreshSystemPrompt(prepared.Session);
        AddUserMessage(prepared.Session, prepared.ComposedMessage);

        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        var settings = CreateExecutionSettings(
            prepared.EffectiveToolNames,
            prepared.EffectiveModelId,
            prepared.EffectiveTemperature);

        var fullResponse = new StringBuilder();

        await foreach (var chunk in chatCompletion.GetStreamingChatMessageContentsAsync(
            prepared.Session.History,
            settings,
            kernel,
            cancellationToken))
        {
            var text = chunk.Content;
            if (string.IsNullOrEmpty(text))
            {
                continue;
            }

            fullResponse.Append(text);
            yield return new ChatStreamEvent(Type: "chunk", Text: text);
        }

        var assistantMessage = fullResponse.ToString();
        prepared.Session.History.AddAssistantMessage(assistantMessage);

        Console.WriteLine($"[Middleware] Stream completed: {assistantMessage.Length} chars");

        yield return new ChatStreamEvent(
            Type: "done",
            ModelId: prepared.EffectiveModelId,
            Temperature: prepared.EffectiveTemperature,
            Message: assistantMessage);
    }

    private async Task<string> ExecuteChatAsync(
        PreparedTurn prepared,
        CancellationToken cancellationToken)
    {
        AddUserMessage(prepared.Session, prepared.ComposedMessage);

        var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
        var response = await chatCompletion.GetChatMessageContentAsync(
            prepared.Session.History,
            CreateExecutionSettings(
                prepared.EffectiveToolNames,
                prepared.EffectiveModelId,
                prepared.EffectiveTemperature),
            kernel,
            cancellationToken);

        prepared.Session.History.Add(response);
        return response.Content ?? string.Empty;
    }

    private PreparedTurn PrepareTurn(
        string message,
        IEnumerable<string>? toolNames,
        string? modelId,
        float? temperature,
        IEnumerable<string>? fileIds)
    {
        var session = sessionManager.GetActiveSession();
        var composedMessage = attachmentService.ComposeMessageWithAttachments(session, message, fileIds);

        return new PreparedTurn(
            session,
            composedMessage,
            ResolveModelId(modelId, session),
            ResolveTemperature(temperature, session),
            toolNames ?? session.SelectedToolNames);
    }

    private static void AddUserMessage(ChatSession session, string message)
    {
        if (string.IsNullOrWhiteSpace(session.Title))
        {
            session.Title = TruncateTitle(message);
        }

        session.History.AddUserMessage(message);
    }

    private void RefreshSystemPrompt(ChatSession session)
    {
        var prompt = systemPromptBuilder.Build();

        if (session.History.Count == 0 || session.History[0].Role != AuthorRole.System)
        {
            session.History.Insert(0, new ChatMessageContent(AuthorRole.System, prompt));
            return;
        }

        session.History[0].Content = prompt;
    }

    private string ResolveModelId(string? requestModelId, ChatSession session)
    {
        if (!string.IsNullOrWhiteSpace(requestModelId))
        {
            return requestModelId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(session.ModelId))
        {
            return session.ModelId;
        }

        return ollamaSettings.ModelId;
    }

    private float ResolveTemperature(float? requestTemperature, ChatSession session)
    {
        if (requestTemperature.HasValue)
        {
            return NormalizeTemperature(requestTemperature.Value);
        }

        if (session.Temperature.HasValue)
        {
            return NormalizeTemperature(session.Temperature.Value);
        }

        return NormalizeTemperature(ollamaSettings.Temperature);
    }

    private static float NormalizeTemperature(float temperature) =>
        Math.Clamp(temperature, 0f, 2f);

    private static string TruncateTitle(string message)
    {
        var trimmed = message.Trim();
        return trimmed.Length <= 40 ? trimmed : $"{trimmed[..37]}...";
    }

    private OllamaPromptExecutionSettings CreateExecutionSettings(
        IEnumerable<string>? toolNames,
        string modelId,
        float temperature)
    {
        var settings = new OllamaPromptExecutionSettings
        {
            ModelId = modelId,
            Temperature = temperature
        };

        if (!chatOptions.ToolCallingEnabled)
        {
            return settings;
        }

        var options = new FunctionChoiceBehaviorOptions
        {
            RetainArgumentTypes = true
        };

        var requiredToolNames = toolNames?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToList();

        if (requiredToolNames is { Count: > 0 })
        {
            var requiredTools = ToolNameResolver.Resolve(kernel, requiredToolNames);
            settings.FunctionChoiceBehavior = FunctionChoiceBehavior.Required(requiredTools, options: options);
            return settings;
        }

        settings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(options: options);
        return settings;
    }

    private sealed record PreparedTurn(
        ChatSession Session,
        string ComposedMessage,
        string EffectiveModelId,
        float EffectiveTemperature,
        IEnumerable<string>? EffectiveToolNames);
}
