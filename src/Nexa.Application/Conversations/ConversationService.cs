using FluentValidation;
using Nexa.Application.Abstractions;
using Nexa.Application.Agents;
using Nexa.Application.Common;
using Nexa.Contracts.Conversations;
using Nexa.Domain.Agents;
using Nexa.Domain.Conversations;

namespace Nexa.Application.Conversations;

public interface IConversationService
{
    Task<IReadOnlyList<ConversationDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken);
    Task<ConversationDetailDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<ConversationDto> CreateAsync(CreateConversationRequest request, CancellationToken cancellationToken);
    Task<ConversationDto> RenameAsync(Guid id, RenameConversationRequest request, CancellationToken cancellationToken);
    Task ArchiveAsync(Guid id, CancellationToken cancellationToken);
    Task<SendMessageResponse> SendMessageAsync(Guid conversationId, SendMessageRequest request, CancellationToken cancellationToken);
}

public sealed class ConversationService(
    IConversationRepository conversations,
    IAgentRepository agents,
    IModelCatalogRepository models,
    IChatCompletionService chat,
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
        await sendValidator.EnsureValidAsync(request, cancellationToken);
        var conversation = await GetOwnedAsync(conversationId, cancellationToken);
        if (conversation.IsArchived)
        {
            throw new NexaException(ErrorCodes.ValidationFailed, "Archived conversations cannot accept messages.");
        }

        var userMessage = conversation.AddMessage(MessageRole.User, request.Content);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var (_, version) = await ResolveAgentAsync(conversation.AgentVersionId, cancellationToken);
        var profile = await models.GetProfileAsync(version.ModelProfileId, cancellationToken)
            ?? throw NexaException.NotFound("Model profile was not found.");
        var provider = await models.GetProviderAsync(profile.ProviderId, cancellationToken)
            ?? throw NexaException.NotFound("Model provider was not found.");

        var history = conversation.Messages
            .OrderBy(m => m.CreatedAt)
            .Where(m => m.Role is MessageRole.User or MessageRole.Assistant)
            .Select(m => (m.Role.ToString(), m.Content))
            .ToList();

        ChatCompletionResult completion;
        try
        {
            completion = await chat.CompleteAsync(version.Instructions, history, provider, profile, cancellationToken);
        }
        catch (NexaException)
        {
            conversation.AddMessage(MessageRole.Error, "The model could not complete this turn.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        var assistant = conversation.AddMessage(
            MessageRole.Assistant,
            completion.Text,
            new TokenUsage(completion.InputTokens, completion.OutputTokens, completion.TotalTokens),
            completion.LatencyMs);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new SendMessageResponse(ConversationMapper.ToDto(userMessage), ConversationMapper.ToDto(assistant));
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
