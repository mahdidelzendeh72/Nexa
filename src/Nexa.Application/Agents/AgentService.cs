using FluentValidation;
using Nexa.Application.Abstractions;
using Nexa.Application.Common;
using Nexa.Contracts.Agents;
using Nexa.Domain.Agents;

namespace Nexa.Application.Agents;

public interface IAgentService
{
    Task<IReadOnlyList<AgentDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken);
    Task<AgentDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<AgentVersionDto>> ListVersionsAsync(Guid agentId, CancellationToken cancellationToken);
    Task<AgentDto> CreateAsync(CreateAgentRequest request, CancellationToken cancellationToken);
    Task<AgentDto> UpdateAsync(Guid id, UpdateAgentRequest request, CancellationToken cancellationToken);
    Task<AgentDto> PublishVersionAsync(Guid id, PublishAgentVersionRequest request, CancellationToken cancellationToken);
    Task ArchiveAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class AgentService(
    IAgentRepository agents,
    IModelCatalogRepository models,
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IValidator<CreateAgentRequest> createValidator,
    IValidator<UpdateAgentRequest> updateValidator,
    IValidator<PublishAgentVersionRequest> publishValidator) : IAgentService
{
    public async Task<IReadOnlyList<AgentDto>> ListAsync(bool includeArchived, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var list = await agents.ListByOwnerAsync(userId, includeArchived, cancellationToken);
        return list.Select(AgentMapper.ToDto).ToList();
    }

    public async Task<AgentDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var agent = await GetOwnedAsync(id, cancellationToken);
        return AgentMapper.ToDto(agent);
    }

    public async Task<IReadOnlyList<AgentVersionDto>> ListVersionsAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var agent = await GetOwnedAsync(agentId, cancellationToken);
        return agent.Versions
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new AgentVersionDto(v.Id, v.VersionNumber, v.ModelProfileId, v.Instructions, v.CreatedAt))
            .ToList();
    }

    public async Task<AgentDto> CreateAsync(CreateAgentRequest request, CancellationToken cancellationToken)
    {
        await createValidator.EnsureValidAsync(request, cancellationToken);
        await EnsureProfileAsync(request.ModelProfileId, cancellationToken);

        var agent = Agent.Create(
            currentUser.RequireUserId(),
            request.Name,
            request.Description ?? string.Empty,
            request.Instructions,
            request.ModelProfileId,
            ChatLimits.Create(request.MaxDurationSeconds, request.MaxToolCalls, request.TokenBudget));

        await agents.AddAsync(agent, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AgentMapper.ToDto(agent);
    }

    public async Task<AgentDto> UpdateAsync(Guid id, UpdateAgentRequest request, CancellationToken cancellationToken)
    {
        await updateValidator.EnsureValidAsync(request, cancellationToken);
        var agent = await GetOwnedAsync(id, cancellationToken);
        agent.Rename(request.Name, request.Description ?? string.Empty);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AgentMapper.ToDto(agent);
    }

    public async Task<AgentDto> PublishVersionAsync(Guid id, PublishAgentVersionRequest request, CancellationToken cancellationToken)
    {
        await publishValidator.EnsureValidAsync(request, cancellationToken);
        await EnsureProfileAsync(request.ModelProfileId, cancellationToken);
        var agent = await GetOwnedAsync(id, cancellationToken);
        agent.PublishVersion(
            request.Instructions,
            request.ModelProfileId,
            ChatLimits.Create(request.MaxDurationSeconds, request.MaxToolCalls, request.TokenBudget));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return AgentMapper.ToDto(agent);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        var agent = await GetOwnedAsync(id, cancellationToken);
        agent.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<Agent> GetOwnedAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var agent = await agents.GetByIdAsync(id, cancellationToken)
            ?? throw NexaException.NotFound("Agent was not found.");

        if (agent.OwnerUserId != userId && !currentUser.Roles.Contains(NexaRoles.Admin, StringComparer.Ordinal))
        {
            throw NexaException.Forbidden("You cannot access this agent.");
        }

        return agent;
    }

    private async Task EnsureProfileAsync(Guid modelProfileId, CancellationToken cancellationToken)
    {
        var profile = await models.GetProfileAsync(modelProfileId, cancellationToken)
            ?? throw NexaException.NotFound("Model profile was not found.");

        if (!profile.IsEnabled)
        {
            throw new NexaException(ErrorCodes.ValidationFailed, "The selected model profile is disabled.");
        }
    }
}
