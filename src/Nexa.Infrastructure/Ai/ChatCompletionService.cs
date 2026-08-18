using System.ClientModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Nexa.Application.Abstractions;
using Nexa.Application.Common;
using Nexa.Domain.Models;
using OpenAI;

namespace Nexa.Infrastructure.Ai;

internal sealed class ChatCompletionService(
    IProviderConnectionResolver connections,
    ILogger<ChatCompletionService> logger) : IChatCompletionService
{
    public async Task<ChatCompletionResult> CompleteAsync(
        string instructions,
        IReadOnlyList<(string Role, string Content)> history,
        ModelProvider provider,
        ModelProfile profile,
        CancellationToken cancellationToken)
    {
        if (!provider.IsEnabled || !profile.IsEnabled)
        {
            throw new NexaException(ErrorCodes.ModelProviderNotConfigured, "The selected model is disabled.", 400);
        }

        if (!IsOpenAiCompatible(provider.Kind))
        {
            throw new NexaException(
                ErrorCodes.ModelProviderUnsupported,
                $"Provider kind '{provider.Kind}' is not available in Phase 1. Use an OpenAI-compatible endpoint.",
                501);
        }

        var connection = connections.Resolve(provider);
        IChatClient client = CreateClient(profile.ModelId, connection);

        var messages = new List<ChatMessage> { new(ChatRole.System, instructions) };
        foreach (var (role, content) in history)
        {
            messages.Add(new ChatMessage(ParseRole(role), content));
        }

        var chatOptions = new ChatOptions
        {
            Temperature = profile.Temperature is null ? null : (float)profile.Temperature.Value,
            MaxOutputTokens = profile.MaxTokens
        };

        var clock = Stopwatch.StartNew();
        ChatResponse response;
        try
        {
            response = await client.GetResponseAsync(messages, chatOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Model completion failed for provider {Provider} model {Model}", provider.Name, profile.ModelId);
            throw new NexaException(ErrorCodes.ChatFailed, "The model provider failed to complete the request.", 502);
        }

        clock.Stop();
        var text = response.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new NexaException(ErrorCodes.ChatFailed, "The model returned an empty response.", 502);
        }

        return new ChatCompletionResult(
            text,
            ToInt(response.Usage?.InputTokenCount),
            ToInt(response.Usage?.OutputTokenCount),
            ToInt(response.Usage?.TotalTokenCount),
            (int)clock.ElapsedMilliseconds);
    }

    private static int? ToInt(long? value) => value is null ? null : (int)Math.Min(value.Value, int.MaxValue);

    private static IChatClient CreateClient(string modelId, ProviderConnection connection)
    {
        var options = new OpenAIClientOptions { Endpoint = new Uri(connection.Endpoint, UriKind.Absolute) };
        var credential = new ApiKeyCredential(string.IsNullOrWhiteSpace(connection.ApiKey) ? "not-needed" : connection.ApiKey);
        return new OpenAIClient(credential, options).GetChatClient(modelId).AsIChatClient();
    }

    private static bool IsOpenAiCompatible(ModelProviderKind kind) =>
        kind is ModelProviderKind.OpenAICompatible
            or ModelProviderKind.OpenAI
            or ModelProviderKind.AzureOpenAI
            or ModelProviderKind.Ollama;

    private static ChatRole ParseRole(string role) =>
        role.Equals("Assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant : ChatRole.User;
}
