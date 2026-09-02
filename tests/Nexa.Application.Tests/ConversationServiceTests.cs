using FluentAssertions;
using Nexa.Application.Abstractions;
using Nexa.Application.Common;
using Nexa.Application.Conversations;
using Nexa.Application.Tools;
using Nexa.Contracts.Conversations;
using Nexa.Domain.Agents;
using Nexa.Domain.AgentRuntime;
using Nexa.Domain.Conversations;

namespace Nexa.Application.Tests;

public sealed class ConversationServiceTests
{
    [Fact]
    public async Task GetAsync_rejects_conversations_owned_by_another_user()
    {
        var owner = new FakeCurrentUser();
        var stranger = new FakeCurrentUser();
        var catalog = new InMemoryCatalog();
        var provider = catalog.AddProvider();
        var profile = catalog.AddProfile(provider.Id);
        var agents = new InMemoryAgents();
        var agent = Agent.Create(owner.UserId, "Support", "", "Help the user.", profile.Id);
        agents.Add(agent);
        var conversations = new InMemoryConversations();
        await conversations.AddAsync(Conversation.Create(owner.UserId, agent.CurrentVersion.Id), CancellationToken.None);

        var ownerService = CreateService(owner, agents, conversations, catalog);
        var created = await ownerService.CreateAsync(new CreateConversationRequest(agent.Id, "Mine"), CancellationToken.None);

        var strangerService = CreateService(stranger, agents, conversations, catalog);
        var act = async () => await strangerService.GetAsync(created.Id, CancellationToken.None);

        var exception = await act.Should().ThrowAsync<NexaException>();
        exception.Which.Code.Should().Be(ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task SendMessage_persists_user_and_assistant_turns()
    {
        var user = new FakeCurrentUser();
        var catalog = new InMemoryCatalog();
        var provider = catalog.AddProvider();
        var profile = catalog.AddProfile(provider.Id);
        var agents = new InMemoryAgents();
        var agent = Agent.Create(user.UserId, "Support", "", "Help the user.", profile.Id);
        agents.Add(agent);
        var conversations = new InMemoryConversations();
        var service = CreateService(user, agents, conversations, catalog);

        var conversation = await service.CreateAsync(new CreateConversationRequest(agent.Id, null), CancellationToken.None);
        var response = await service.SendMessageAsync(conversation.Id, new SendMessageRequest("Hi"), CancellationToken.None);

        response.UserMessage.Content.Should().Be("Hi");
        response.AssistantMessage.Content.Should().Be("hello from nexa");
        var detail = await service.GetAsync(conversation.Id, CancellationToken.None);
        detail.Messages.Should().HaveCount(2);
        detail.Conversation.AgentVersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task StreamMessage_emits_deltas_then_completes_the_run()
    {
        var user = new FakeCurrentUser();
        var catalog = new InMemoryCatalog();
        var provider = catalog.AddProvider();
        var profile = catalog.AddProfile(provider.Id);
        var agents = new InMemoryAgents();
        var agent = Agent.Create(user.UserId, "Support", "", "Help the user.", profile.Id);
        agents.Add(agent);
        var conversations = new InMemoryConversations();
        var runs = new InMemoryAgentRuns();
        var service = CreateService(user, agents, conversations, catalog, new FakeRuntime(), runs);

        var conversation = await service.CreateAsync(new CreateConversationRequest(agent.Id, null), CancellationToken.None);
        var events = new List<string>();
        await foreach (var evt in service.StreamMessageAsync(conversation.Id, new SendMessageRequest("Hi"), CancellationToken.None))
        {
            events.Add(evt.Kind);
        }

        events.Should().Equal("RunStarted", "TextDelta", "RunCompleted");
        var stored = await conversations.GetByIdAsync(conversation.Id, CancellationToken.None);
        stored!.RuntimeSessionState.Should().Be("""{"ok":true}""");
        runs.Items.Should().ContainSingle(r => r.Status == AgentRunStatus.Completed);
    }

    [Fact]
    public async Task StreamMessage_persists_tool_calls_without_chain_of_thought()
    {
        var user = new FakeCurrentUser();
        var catalog = new InMemoryCatalog();
        var provider = catalog.AddProvider();
        var profile = catalog.AddProfile(provider.Id);
        var agents = new InMemoryAgents();
        var agent = Agent.Create(user.UserId, "Support", "", "Help the user.", profile.Id);
        agents.Add(agent);
        var conversations = new InMemoryConversations();
        var service = CreateService(user, agents, conversations, catalog, new FakeRuntimeWithTools());

        var conversation = await service.CreateAsync(new CreateConversationRequest(agent.Id, null), CancellationToken.None);
        await service.SendMessageAsync(conversation.Id, new SendMessageRequest("What time is it?"), CancellationToken.None);

        var detail = await service.GetAsync(conversation.Id, CancellationToken.None);
        detail.Messages.Select(m => m.Role).Should().Equal("User", "ToolCall", "ToolResult", "Assistant");
        var runs = await service.ListRunsAsync(conversation.Id, CancellationToken.None);
        runs.Should().ContainSingle();
        runs[0].ToolExecutions.Should().ContainSingle(t => t.ToolName == "utc_now" && t.Result != null);
    }

    private static ConversationService CreateService(
        FakeCurrentUser user,
        InMemoryAgents agents,
        InMemoryConversations conversations,
        InMemoryCatalog catalog,
        IAgentRuntime? runtime = null,
        InMemoryAgentRuns? runs = null) =>
        new(
            conversations,
            agents,
            catalog,
            runtime ?? new FakeRuntime(),
            runs ?? new InMemoryAgentRuns(),
            new FakeToolRegistry(),
            user,
            new InMemoryUnitOfWork(),
            new CreateConversationRequestValidator(),
            new RenameConversationRequestValidator(),
            new SendMessageRequestValidator());
}

public sealed class ToolCatalogServiceTests
{
    [Fact]
    public void List_returns_registry_descriptors()
    {
        var service = new ToolCatalogService(new FakeToolRegistry());
        service.List().Should().ContainSingle(t => t.Id == "utc_now" && !t.RequiresApproval);
    }
}
