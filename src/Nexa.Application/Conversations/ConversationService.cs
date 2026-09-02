using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using FluentValidation;
using Nexa.Application.Abstractions;
using Nexa.Application.Agents;
using Nexa.Application.Common;
using Nexa.Contracts.Conversations;
using Nexa.Contracts.Runtime;
using Nexa.Domain.Agents;
using Nexa.Domain.AgentRuntime;
using Nexa.Domain.Conversations;

namespace Nexa.Application.Conversations;

public sealed class ConversationService(
    IConversationRepository conversations,
    IAgentRepository agents,
    IModelCatalogRepository models,
    IAgentRuntime runtime,
    IAgentRunRepository runs,
    IToolRegistry tools,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IValidator<CreateConversationRequest> createValidator,
    IValidator<RenameConversationRequest> renameValidator,
    IValidator<SendMessageRequest> sendValidator) : IConversationService
{
    public async Task<IReadOnlyList<ConversationDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var list = await conversations.ListByUserAsync(userId, includeArchived, cancellationToken);
        var agentsByVersion = await LoadAgentsAsync(list.Select(c => c.AgentVersionId).Distinct().ToList(), cancellationToken);
        return list.Select(conversation =>
        {
            var (agent, version) = agentsByVersion[conversation.AgentVersionId];
            return ConversationMapper.ToDto(conversation, agent, version);
        }).ToList();
    }

    public async Task<ConversationDetailDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var conversation = await GetOwnedAsync(id, cancellationToken);
        var (agent, version) = await ResolveAgentAsync(conversation.AgentVersionId, cancellationToken);
        return new ConversationDetailDto(
            ConversationMapper.ToDto(conversation, agent, version),
            conversation.Messages.OrderBy(m => m.CreatedAt).Select(ConversationMapper.ToDto).ToList());
    }

    public async Task<ConversationDto> CreateAsync(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);
        var userId = currentUser.RequireUserId();
        var agent = await agents.GetByIdAsync(request.AgentId, cancellationToken)
            ?? throw NexaException.NotFound("Agent was not found.");

        if (agent.OwnerUserId != userId && !currentUser.Roles.Contains(NexaRoles.Admin, StringComparer.Ordinal))
        {
            throw NexaException.Forbidden("You cannot start a conversation with this agent.");
        }

        if (agent.IsArchived)
        {
            throw new NexaException(ErrorCodes.ValidationFailed, "Archived agents cannot start conversations.");
        }

        var conversation = Conversation.Create(userId, agent.CurrentVersion.Id, request.Title);
        await conversations.AddAsync(conversation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ConversationMapper.ToDto(conversation, agent, agent.CurrentVersion);
    }

    public async Task<ConversationDto> RenameAsync(Guid id, RenameConversationRequest request, CancellationToken cancellationToken)
    {
        await renameValidator.EnsureValidAsync(request, cancellationToken);
        var conversation = await GetOwnedAsync(id, cancellationToken);
        conversation.Rename(request.Title);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var (agent, version) = await ResolveAgentAsync(conversation.AgentVersionId, cancellationToken);
        return ConversationMapper.ToDto(conversation, agent, version);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var conversation = await GetOwnedAsync(id, cancellationToken);
        conversation.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SendMessageResponse> SendMessageAsync(Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken)
    {
        AgentStreamEventDto? error = null;
        await foreach (var evt in StreamMessageAsync(conversationId, request, cancellationToken))
        {
            if (evt.Kind == nameof(AgentRuntimeEventKind.Error))
            {
                error = evt;
            }
        }

        if (error is not null)
        {
            throw new NexaException(
                error.ErrorCode ?? ErrorCodes.ChatFailed,
                error.ErrorMessage ?? "The agent run failed.",
                502);
        }

        var detail = await GetAsync(conversationId, cancellationToken);
        var user = detail.Messages.LastOrDefault(m => m.Role == nameof(MessageRole.User))
            ?? throw new NexaException(ErrorCodes.ChatFailed, "The user message was not persisted.", 500);
        var assistant = detail.Messages.LastOrDefault(m => m.Role is nameof(MessageRole.Assistant) or nameof(MessageRole.Error))
            ?? throw new NexaException(ErrorCodes.ChatFailed, "The assistant message was not persisted.", 502);
        return new SendMessageResponse(user, assistant);
    }

    public async IAsyncEnumerable<AgentStreamEventDto> StreamMessageAsync(
        Guid conversationId,
        SendMessageRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await sendValidator.EnsureValidAsync(request, cancellationToken);
        var conversation = await GetOwnedAsync(conversationId, cancellationToken);
        if (conversation.IsArchived)
        {
            throw new NexaException(ErrorCodes.ValidationFailed, "Archived conversations cannot accept messages.");
        }

        var userId = currentUser.RequireUserId();
        conversation.AddMessage(MessageRole.User, request.Content);
        var (agent, version) = await ResolveAgentAsync(conversation.AgentVersionId, cancellationToken);
        var profile = await models.GetProfileAsync(version.ModelProfileId, cancellationToken)
            ?? throw NexaException.NotFound("Model profile was not found.");
        var provider = await models.GetProviderAsync(profile.ProviderId, cancellationToken)
            ?? throw NexaException.NotFound("Model provider was not found.");

        var run = AgentRun.Start(conversation.Id, version.Id, userId, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"));
        await runs.AddAsync(run, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        yield return new AgentStreamEventDto(nameof(AgentRuntimeEventKind.RunStarted), RunId: run.Id);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (version.MaxDurationSeconds is int seconds)
        {
            linked.CancelAfter(TimeSpan.FromSeconds(seconds));
        }

        var history = conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .Where(m => m.Role is MessageRole.User or MessageRole.Assistant or MessageRole.System)
            .Select(m => new AgentRuntimeMessage(m.Role.ToString(), m.Content))
            .ToList();

        var maxOutputTokens = profile.MaxTokens;
        if (version.TokenBudget is int budget)
        {
            maxOutputTokens = maxOutputTokens is int existing ? Math.Min(existing, budget) : budget;
        }

        var runtimeRequest = new AgentRuntimeRequest(
            agent.Name,
            version.Instructions,
            history,
            provider,
            profile,
            conversation.RuntimeSessionState,
            tools.List().Select(t => t.Id).ToList(),
            maxOutputTokens);

        var assistantText = new StringBuilder();
        var clock = Stopwatch.StartNew();
        var completed = false;

        IAsyncEnumerator<AgentRuntimeEvent>? enumerator = null;
        try
        {
            enumerator = runtime.RunStreamingAsync(runtimeRequest, linked.Token).GetAsyncEnumerator(linked.Token);
            while (true)
            {
                AgentRuntimeEvent? evt = null;
                var timedOut = false;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        break;
                    }

                    evt = enumerator.Current;
                }
                catch (OperationCanceledException)
                {
                    clock.Stop();
                    if (run.Status == AgentRunStatus.Running)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            run.Cancel((int)clock.ElapsedMilliseconds);
                            conversation.AddMessage(MessageRole.Error, "The run was cancelled.");
                        }
                        else
                        {
                            run.Fail(ErrorCodes.ChatFailed, "The run exceeded the maximum duration.", (int)clock.ElapsedMilliseconds);
                            conversation.AddMessage(MessageRole.Error, "The run exceeded the maximum duration.");
                        }

                        await unitOfWork.SaveChangesAsync(CancellationToken.None);
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }

                    timedOut = true;
                }

                if (timedOut)
                {
                    yield return new AgentStreamEventDto(
                        nameof(AgentRuntimeEventKind.Error),
                        RunId: run.Id,
                        ErrorCode: ErrorCodes.ChatFailed,
                        ErrorMessage: "The run exceeded the maximum duration.",
                        LatencyMs: (int)clock.ElapsedMilliseconds);
                    yield break;
                }

                switch (evt!.Kind)
                {
                    case AgentRuntimeEventKind.TextDelta when !string.IsNullOrEmpty(evt.Text):
                        assistantText.Append(evt.Text);
                        yield return new AgentStreamEventDto(
                            nameof(AgentRuntimeEventKind.TextDelta),
                            RunId: run.Id,
                            Text: evt.Text);
                        break;

                    case AgentRuntimeEventKind.ToolCallStarted:
                        run.RecordToolStart(evt.ToolName ?? "unknown", evt.ToolCallId, evt.ToolArguments);
                        conversation.AddMessage(
                            MessageRole.ToolCall,
                            FormatToolPayload(evt.ToolName, evt.ToolArguments));
                        await unitOfWork.SaveChangesAsync(linked.Token);
                        yield return new AgentStreamEventDto(
                            nameof(AgentRuntimeEventKind.ToolCallStarted),
                            RunId: run.Id,
                            ToolName: evt.ToolName,
                            ToolCallId: evt.ToolCallId,
                            ToolArguments: evt.ToolArguments);
                        break;

                    case AgentRuntimeEventKind.ToolCallCompleted:
                        CompleteToolExecution(run, evt);
                        conversation.AddMessage(
                            MessageRole.ToolResult,
                            FormatToolPayload(evt.ToolName, evt.ToolResult));
                        await unitOfWork.SaveChangesAsync(linked.Token);
                        yield return new AgentStreamEventDto(
                            nameof(AgentRuntimeEventKind.ToolCallCompleted),
                            RunId: run.Id,
                            ToolName: evt.ToolName,
                            ToolCallId: evt.ToolCallId,
                            ToolResult: evt.ToolResult);
                        break;

                    case AgentRuntimeEventKind.RunCompleted:
                        clock.Stop();
                        PersistAssistant(conversation, assistantText.ToString(), evt);
                        conversation.SetRuntimeSessionState(evt.SessionState);
                        run.Complete(
                            evt.InputTokens,
                            evt.OutputTokens,
                            evt.TotalTokens,
                            evt.LatencyMs ?? (int)clock.ElapsedMilliseconds);
                        await unitOfWork.SaveChangesAsync(linked.Token);
                        completed = true;
                        yield return new AgentStreamEventDto(
                            nameof(AgentRuntimeEventKind.RunCompleted),
                            RunId: run.Id,
                            InputTokens: evt.InputTokens,
                            OutputTokens: evt.OutputTokens,
                            TotalTokens: evt.TotalTokens,
                            LatencyMs: evt.LatencyMs ?? (int)clock.ElapsedMilliseconds);
                        break;

                    case AgentRuntimeEventKind.Error:
                        clock.Stop();
                        if (run.Status == AgentRunStatus.Running)
                        {
                            run.Fail(
                                evt.ErrorCode ?? ErrorCodes.ChatFailed,
                                evt.ErrorMessage ?? "The agent run failed.",
                                evt.LatencyMs ?? (int)clock.ElapsedMilliseconds);
                            conversation.AddMessage(MessageRole.Error, evt.ErrorMessage ?? "The agent run failed.");
                            await unitOfWork.SaveChangesAsync(CancellationToken.None);
                        }

                        yield return new AgentStreamEventDto(
                            nameof(AgentRuntimeEventKind.Error),
                            RunId: run.Id,
                            ErrorCode: evt.ErrorCode ?? ErrorCodes.ChatFailed,
                            ErrorMessage: evt.ErrorMessage ?? "The agent run failed.",
                            LatencyMs: evt.LatencyMs ?? (int)clock.ElapsedMilliseconds);
                        completed = true;
                        yield break;
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

        if (!completed && run.Status == AgentRunStatus.Running)
        {
            clock.Stop();
            run.Fail(ErrorCodes.ChatFailed, "The agent run ended without a completion event.", (int)clock.ElapsedMilliseconds);
            conversation.AddMessage(MessageRole.Error, "The agent run ended without a completion event.");
            await unitOfWork.SaveChangesAsync(CancellationToken.None);
            yield return new AgentStreamEventDto(
                nameof(AgentRuntimeEventKind.Error),
                RunId: run.Id,
                ErrorCode: ErrorCodes.ChatFailed,
                ErrorMessage: "The agent run ended without a completion event.",
                LatencyMs: (int)clock.ElapsedMilliseconds);
        }
    }

    public async Task<IReadOnlyList<AgentRunDto>> ListRunsAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        await GetOwnedAsync(conversationId, cancellationToken);
        var list = await runs.ListByConversationAsync(conversationId, cancellationToken);
        return list.Select(AgentRunMapper.ToDto).ToList();
    }

    private static void PersistAssistant(Conversation conversation, string text, AgentRuntimeEvent evt)
    {
        var content = string.IsNullOrWhiteSpace(text)
            ? "The agent completed without a text reply."
            : text;
        conversation.AddMessage(
            MessageRole.Assistant,
            content,
            new TokenUsage(evt.InputTokens, evt.OutputTokens, evt.TotalTokens),
            evt.LatencyMs);
    }

    private static void CompleteToolExecution(AgentRun run, AgentRuntimeEvent evt)
    {
        var pending = run.ToolExecutions.LastOrDefault(t =>
                          t.CompletedAt is null
                          && (evt.ToolCallId is null || t.CallId == evt.ToolCallId))
                      ?? run.ToolExecutions.LastOrDefault(t => t.CompletedAt is null && t.ToolName == evt.ToolName);

        if (pending is null)
        {
            pending = run.RecordToolStart(evt.ToolName ?? "unknown", evt.ToolCallId, evt.ToolArguments);
        }

        pending.Complete(evt.ToolResult, DateTimeOffset.UtcNow);
    }

    private static string FormatToolPayload(string? name, string? payload)
    {
        var text = $"{name ?? "tool"}: {payload ?? "(empty)"}";
        return text.Length <= 100_000 ? text : text[..100_000];
    }

    private async Task<Conversation> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var conversation = await conversations.GetByIdAsync(id, cancellationToken)
            ?? throw NexaException.NotFound("Conversation was not found.");

        if (conversation.UserId != userId)
        {
            throw NexaException.Forbidden("You cannot access this conversation.");
        }

        return conversation;
    }

    private async Task<(Agent Agent, AgentVersion Version)> ResolveAgentAsync(Guid agentVersionId, CancellationToken cancellationToken)
    {
        var map = await LoadAgentsAsync([agentVersionId], cancellationToken);
        if (!map.TryGetValue(agentVersionId, out var pair))
        {
            throw NexaException.NotFound("The agent version for this conversation was not found.");
        }

        return pair;
    }

    private async Task<Dictionary<Guid, (Agent Agent, AgentVersion Version)>> LoadAgentsAsync(
        IReadOnlyCollection<Guid> versionIds,
        CancellationToken cancellationToken)
    {
        var loaded = await agents.ListByVersionIdsAsync(versionIds, cancellationToken);
        var map = new Dictionary<Guid, (Agent Agent, AgentVersion Version)>();
        foreach (var agent in loaded)
        {
            foreach (var version in agent.Versions)
            {
                if (versionIds.Contains(version.Id))
                {
                    map[version.Id] = (agent, version);
                }
            }
        }

        return map;
    }
}
