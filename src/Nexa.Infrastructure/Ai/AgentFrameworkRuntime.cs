using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Nexa.Application.Abstractions;
using Nexa.Application.Common;

namespace Nexa.Infrastructure.Ai;

internal sealed class AgentFrameworkRuntime(
    IChatClientFactory chatClients,
    BuiltInToolCatalog tools,
    ILoggerFactory loggerFactory,
    IServiceProvider services,
    ILogger<AgentFrameworkRuntime> logger) : IAgentRuntime
{
    public async IAsyncEnumerable<AgentRuntimeEvent> RunStreamingAsync(
        AgentRuntimeRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var (setup, error) = await TryCreateAsync(request, cancellationToken);
        if (error is not null)
        {
            yield return error;
            yield break;
        }

        var (_, agent, session, restored, aiTools) = setup!;
        var messages = BuildMessages(request.History, restored);
        var chatOptions = new ChatOptions
        {
            Temperature = request.Profile.Temperature is null ? null : (float)request.Profile.Temperature.Value,
            MaxOutputTokens = request.MaxOutputTokens ?? request.Profile.MaxTokens,
            Tools = aiTools.Count == 0 ? null : aiTools
        };
        var options = new ChatClientAgentRunOptions(chatOptions);

        var clock = Stopwatch.StartNew();
        var tokenTotals = new TokenTotals();

        IAsyncEnumerator<AgentResponseUpdate>? enumerator = null;
        try
        {
            enumerator = agent.RunStreamingAsync(messages, session, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                AgentResponseUpdate? update = null;
                Exception? runError = null;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }

                    update = enumerator.Current;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    runError = ex;
                }

                if (runError is not null)
                {
                    clock.Stop();
                    logger.LogError(runError, "Agent run failed for provider {Provider} model {Model}", request.Provider.Name, request.Profile.ModelId);
                    yield return new AgentRuntimeEvent(
                        AgentRuntimeEventKind.Error,
                        ErrorCode: ErrorCodes.ChatFailed,
                        ErrorMessage: "The model provider failed to complete the request.",
                        LatencyMs: (int)clock.ElapsedMilliseconds);
                    yield break;
                }

                foreach (var evt in ToRuntimeEvents(update!, tokenTotals))
                {
                    yield return evt;
                }
            }
        }
        finally
        {
            if (enumerator is not null)
            {
                await enumerator.DisposeAsync();
            }
        }

        clock.Stop();
        string? sessionState = null;
        try
        {
            var json = await agent.SerializeSessionAsync(session, cancellationToken: cancellationToken);
            sessionState = json.GetRawText();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to serialize the agent session; the next turn will replay conversation history.");
        }

        yield return new AgentRuntimeEvent(
            AgentRuntimeEventKind.RunCompleted,
            InputTokens: tokenTotals.Input,
            OutputTokens: tokenTotals.Output,
            TotalTokens: tokenTotals.Total,
            LatencyMs: (int)clock.ElapsedMilliseconds,
            SessionState: sessionState);
    }

    private async Task<(RuntimeSetup? Setup, AgentRuntimeEvent? Error)> TryCreateAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken)
    {
        IChatClient client;
        try
        {
            client = chatClients.Create(request.Provider, request.Profile);
        }
        catch (NexaException ex)
        {
            return (null, new AgentRuntimeEvent(AgentRuntimeEventKind.Error, ErrorCode: ex.Code, ErrorMessage: ex.Message));
        }

        var aiTools = tools.ResolveAiTools(request.ToolIds);
        AIAgent agent;
        try
        {
            agent = client.AsAIAgent(
                instructions: request.Instructions,
                name: request.AgentName,
                description: request.AgentName,
                tools: aiTools.Count == 0 ? null : aiTools,
                loggerFactory: loggerFactory,
                services: services);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to create the Agent Framework agent for {Agent}", request.AgentName);
            return (null, new AgentRuntimeEvent(
                AgentRuntimeEventKind.Error,
                ErrorCode: ErrorCodes.ChatFailed,
                ErrorMessage: "The agent runtime could not be created."));
        }

        try
        {
            var (session, restored) = await RestoreOrCreateSessionAsync(agent, request.SessionState, cancellationToken);
            return (new RuntimeSetup(client, agent, session, restored, aiTools), null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to create an agent session for {Agent}", request.AgentName);
            return (null, new AgentRuntimeEvent(
                AgentRuntimeEventKind.Error,
                ErrorCode: ErrorCodes.ChatFailed,
                ErrorMessage: "The agent session could not be created."));
        }
    }

    private sealed record RuntimeSetup(
        IChatClient Client,
        AIAgent Agent,
        AgentSession Session,
        bool Restored,
        IList<AITool> Tools);

    private async Task<(AgentSession Session, bool Restored)> RestoreOrCreateSessionAsync(
        AIAgent agent,
        string? sessionState,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionState))
        {
            return (await agent.CreateSessionAsync(cancellationToken: cancellationToken), false);
        }

        try
        {
            using var document = JsonDocument.Parse(sessionState);
            var session = await agent.DeserializeSessionAsync(document.RootElement.Clone(), cancellationToken: cancellationToken);
            return (session, true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to restore the agent session; starting a new session and replaying history.");
            return (await agent.CreateSessionAsync(cancellationToken: cancellationToken), false);
        }
    }

    private static List<ChatMessage> BuildMessages(IReadOnlyList<AgentRuntimeMessage> history, bool restored)
    {
        if (restored)
        {
            var lastUser = history.LastOrDefault(m => m.Role.Equals("User", StringComparison.OrdinalIgnoreCase));
            return lastUser is null ? [] : [new ChatMessage(ChatRole.User, lastUser.Content)];
        }

        var messages = new List<ChatMessage>();
        foreach (var item in history)
        {
            if (item.Role.Equals("Assistant", StringComparison.OrdinalIgnoreCase))
            {
                messages.Add(new ChatMessage(ChatRole.Assistant, item.Content));
            }
            else if (item.Role.Equals("System", StringComparison.OrdinalIgnoreCase))
            {
                messages.Add(new ChatMessage(ChatRole.System, item.Content));
            }
            else if (item.Role.Equals("User", StringComparison.OrdinalIgnoreCase))
            {
                messages.Add(new ChatMessage(ChatRole.User, item.Content));
            }
        }

        return messages;
    }

    private static List<AgentRuntimeEvent> ToRuntimeEvents(AgentResponseUpdate update, TokenTotals tokens)
    {
        var events = new List<AgentRuntimeEvent>();
        var emittedText = false;

        foreach (var content in update.Contents)
        {
            switch (content)
            {
                case TextReasoningContent:
                    break;
                case TextContent text when !string.IsNullOrEmpty(text.Text):
                    emittedText = true;
                    events.Add(new AgentRuntimeEvent(AgentRuntimeEventKind.TextDelta, Text: text.Text));
                    break;
                case FunctionCallContent call:
                    events.Add(new AgentRuntimeEvent(
                        AgentRuntimeEventKind.ToolCallStarted,
                        ToolName: call.Name,
                        ToolCallId: call.CallId,
                        ToolArguments: Serialize(call.Arguments)));
                    break;
                case FunctionResultContent result:
                    events.Add(new AgentRuntimeEvent(
                        AgentRuntimeEventKind.ToolCallCompleted,
                        ToolCallId: result.CallId,
                        ToolResult: Serialize(result.Result)));
                    break;
                case UsageContent usage:
                    tokens.Input = ToInt(usage.Details.InputTokenCount) ?? tokens.Input;
                    tokens.Output = ToInt(usage.Details.OutputTokenCount) ?? tokens.Output;
                    tokens.Total = ToInt(usage.Details.TotalTokenCount) ?? tokens.Total;
                    break;
            }
        }

        if (!emittedText && !string.IsNullOrEmpty(update.Text) && update.Contents.All(c => c is not TextReasoningContent))
        {
            events.Add(new AgentRuntimeEvent(AgentRuntimeEventKind.TextDelta, Text: update.Text));
        }

        return events;
    }

    private sealed class TokenTotals
    {
        public int? Input { get; set; }
        public int? Output { get; set; }
        public int? Total { get; set; }
    }

    private static string? Serialize(object? value) =>
        value switch
        {
            null => null,
            string text => text,
            JsonElement element => element.GetRawText(),
            _ => JsonSerializer.Serialize(value)
        };

    private static int? ToInt(long? value) => value is null ? null : (int)Math.Min(value.Value, int.MaxValue);
}
