using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nexa.Domain.Agents;
using Nexa.Domain.Conversations;
using Nexa.Domain.Models;
using Nexa.Infrastructure.Persistence;

namespace Nexa.Api.Tests;

public sealed class ConversationPersistenceTests
{
    [Fact]
    public async Task Saving_user_then_assistant_on_the_same_context_does_not_throw_concurrency()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<NexaDbContext>().UseSqlite(connection).Options;
        await using var db = new NexaDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var userId = Guid.CreateVersion7();
        var provider = ModelProvider.Create("Ollama", ModelProviderKind.Ollama);
        var profile = ModelProfile.Create(provider.Id, "Local", "llama3.2", 0.2, 256);
        var agent = Agent.Create(userId, "Support", "", "Help the user.", profile.Id);
        var conversation = Conversation.Create(userId, agent.CurrentVersion.Id);
        db.Add(provider);
        db.Add(profile);
        db.Add(agent);
        db.Add(conversation);
        await db.SaveChangesAsync();

        conversation.AddMessage(MessageRole.User, "Hi");
        await db.SaveChangesAsync();

        conversation.AddMessage(MessageRole.Assistant, "Hello");
        conversation.SetRuntimeSessionState("""{"ok":true}""");
        await db.SaveChangesAsync();

        var reloaded = await db.Conversations.AsNoTracking().Include(c => c.Messages).SingleAsync(c => c.Id == conversation.Id);
        reloaded.Messages.Should().HaveCount(2);
        reloaded.RuntimeSessionState.Should().Be("""{"ok":true}""");
    }
}
