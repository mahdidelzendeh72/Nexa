using Nexa.Application.Abstractions;
using Nexa.Application.Common;
using Nexa.Domain.Agents;
using Nexa.Domain.AgentRuntime;
using Nexa.Domain.Conversations;
using Nexa.Domain.Models;

namespace Nexa.Application.Tests;

internal sealed class FakeCurrentUser : ICurrentUser
{
    public bool IsAuthenticated { get; init; } = true;
    public Guid UserId { get; init; } = Guid.CreateVersion7();
    public string? Email { get; init; } = "user@nexa.local";
    public IReadOnlyCollection<string> Roles { get; init; } = [NexaRoles.User];
}

internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
}

internal sealed class InMemoryAgents : IAgentRepository
{
    private readonly List<Agent> _items = [];

    public void Add(Agent agent) => _items.Add(agent);

    public Task<Agent?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

    public Task<Agent?> GetByVersionIdAsync(Guid agentVersionId, CancellationToken cancellationToken) =>
        Task.FromResult(_items.FirstOrDefault(a => a.Versions.Any(v => v.Id == agentVersionId)));

    public Task<IReadOnlyList<Agent>> ListByOwnerAsync(Guid ownerUserId, bool includeArchived, CancellationToken cancellationToken)
    {
        IEnumerable<Agent> query = _items.Where(x => x.OwnerUserId == ownerUserId);
        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        return Task.FromResult<IReadOnlyList<Agent>>(query.ToList());
    }

    public Task<IReadOnlyList<Agent>> ListByVersionIdsAsync(IReadOnlyCollection<Guid> versionIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Agent>>(_items.Where(a => a.Versions.Any(v => versionIds.Contains(v.Id))).ToList());

    public Task AddAsync(Agent agent, CancellationToken cancellationToken)
    {
        _items.Add(agent);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryConversations : IConversationRepository
{
    private readonly List<Conversation> _items = [];

    public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<Conversation>> ListByUserAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken)
    {
        IEnumerable<Conversation> query = _items.Where(x => x.UserId == userId);
        if (!includeArchived)
        {
            query = query.Where(x => !x.IsArchived);
        }

        return Task.FromResult<IReadOnlyList<Conversation>>(query.ToList());
    }

    public Task AddAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        _items.Add(conversation);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryCatalog : IModelCatalogRepository
{
    private readonly List<ModelProvider> _providers = [];
    private readonly List<ModelProfile> _profiles = [];

    public ModelProvider AddProvider(string name = "Ollama", ModelProviderKind kind = ModelProviderKind.Ollama)
    {
        var provider = ModelProvider.Create(name, kind);
        _providers.Add(provider);
        return provider;
    }

    public ModelProfile AddProfile(Guid providerId, string name = "Local")
    {
        var profile = ModelProfile.Create(providerId, name, "llama3.2", 0.2, 256);
        _profiles.Add(profile);
        return profile;
    }

    public Task<ModelProvider?> GetProviderAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_providers.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<ModelProvider>> ListProvidersAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ModelProvider>>(_providers);

    public Task<ModelProfile?> GetProfileAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_profiles.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<ModelProfile>> ListProfilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ModelProfile>>(_profiles);

    public Task AddProfileAsync(ModelProfile profile, CancellationToken cancellationToken)
    {
        _profiles.Add(profile);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryAgentRuns : IAgentRunRepository
{
    public List<AgentRun> Items { get; } = [];

    public Task<AgentRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

    public Task<IReadOnlyList<AgentRun>> ListByConversationAsync(Guid conversationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AgentRun>>(Items.Where(x => x.ConversationId == conversationId).ToList());

    public Task AddAsync(AgentRun run, CancellationToken cancellationToken)
    {
        Items.Add(run);
        return Task.CompletedTask;
    }
}

internal sealed class FakeToolRegistry : IToolRegistry
{
    public IReadOnlyList<ToolDescriptor> List() =>
    [
        new("utc_now", "utc_now", "Returns UTC now.", "BuiltIn", "1.0.0", true, false, "Low")
    ];

    public IReadOnlyList<ToolDescriptor> ResolveEnabled(IReadOnlyList<string>? selectedIds) => List();
}

internal sealed class FakeRuntime : IAgentRuntime
{
    public async IAsyncEnumerable<AgentRuntimeEvent> RunStreamingAsync(
        AgentRuntimeRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new AgentRuntimeEvent(AgentRuntimeEventKind.TextDelta, Text: "hello from nexa");
        yield return new AgentRuntimeEvent(
            AgentRuntimeEventKind.RunCompleted,
            InputTokens: 10,
            OutputTokens: 4,
            TotalTokens: 14,
            LatencyMs: 12,
            SessionState: """{"ok":true}""");
        await Task.CompletedTask;
    }
}

internal sealed class FakeRuntimeWithTools : IAgentRuntime
{
    public async IAsyncEnumerable<AgentRuntimeEvent> RunStreamingAsync(
        AgentRuntimeRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new AgentRuntimeEvent(
            AgentRuntimeEventKind.ToolCallStarted,
            ToolName: "utc_now",
            ToolCallId: "call-1",
            ToolArguments: "{}");
        yield return new AgentRuntimeEvent(
            AgentRuntimeEventKind.ToolCallCompleted,
            ToolName: "utc_now",
            ToolCallId: "call-1",
            ToolResult: "2026-08-18T00:00:00.0000000+00:00");
        yield return new AgentRuntimeEvent(AgentRuntimeEventKind.TextDelta, Text: "It is midnight UTC.");
        yield return new AgentRuntimeEvent(AgentRuntimeEventKind.RunCompleted, LatencyMs: 20, SessionState: "{}");
        await Task.CompletedTask;
    }
}
