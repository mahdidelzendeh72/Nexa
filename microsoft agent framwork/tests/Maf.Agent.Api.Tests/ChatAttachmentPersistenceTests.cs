using Maf.Agent.Api.Chat.Domain;
using Maf.Agent.Api.Chat.Infrastructure.History;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Tests;

public sealed class ChatAttachmentPersistenceTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-08T08:00:00Z");

    [Fact]
    public async Task Base64_survives_a_later_turn_that_does_not_load_the_files()
    {
        await using ChatPersistenceContext context = await CreateContextAsync();
        context.Conversations.Add(ChatConversation.Open(
            "thread-1",
            "user-1",
            "general",
            "این فایل",
            "دیدم",
            null,
            null,
            At,
            attachments: [new ChatFile("notes.pdf", "application/pdf", "AQID")]));
        await context.SaveChangesAsync();

        await using ChatPersistenceContext appending = await CreateContextAsync(context.Connection);
        ChatConversation stored = await appending.Conversations.Include(item => item.Entries).SingleAsync();
        stored.AppendTurn("user-1", "general", "ادامه", "باشه", null, null, At.AddMinutes(1));
        stored.React("user-1", 2, ChatReaction.Like, At.AddMinutes(1));
        await appending.SaveChangesAsync();

        await using ChatPersistenceContext reading = await CreateContextAsync(context.Connection);
        ChatEntry user = await reading.Set<ChatEntry>()
            .Include(entry => entry.Attachments)
            .SingleAsync(entry => entry.Sequence == 1);
        ChatAttachment file = Assert.Single(user.Attachments);
        Assert.Equal("notes.pdf", file.FileName);
        Assert.Equal("application/pdf", file.MediaType);
        Assert.Equal("AQID", file.ContentBase64);
        Assert.Equal(ChatReaction.Like, (await reading.Set<ChatEntry>().SingleAsync(entry => entry.Sequence == 2)).Reaction);
    }

    [Fact]
    public async Task Token_counts_survive_a_later_turn_that_does_not_load_them()
    {
        ChatTurnUsage usage = ChatTurnUsage.Record(20, 5, 2, 7, 25, 1)!;
        await using ChatPersistenceContext context = await CreateContextAsync();
        context.Conversations.Add(ChatConversation.Open(
            "thread-1",
            "user-1",
            "general",
            "سلام",
            "درود",
            null,
            null,
            At,
            usage: usage));
        await context.SaveChangesAsync();

        await using ChatPersistenceContext appending = await CreateContextAsync(context.Connection);
        ChatConversation stored = await appending.Conversations.Include(item => item.Entries).SingleAsync();
        stored.AppendTurn(
            "user-1",
            "general",
            "ادامه",
            "باشه",
            null,
            null,
            At.AddMinutes(1),
            usage: ChatTurnUsage.Record(3, 1, null, null, 4));
        await appending.SaveChangesAsync();

        await using ChatPersistenceContext reading = await CreateContextAsync(context.Connection);
        ChatEntry assistant = await reading.Set<ChatEntry>().Include(entry => entry.Usage).SingleAsync(entry => entry.Sequence == 2);
        Assert.Equal(assistant.Id, assistant.Usage!.EntryId);
        Assert.Equal(20, assistant.Usage.InputTokens);
        Assert.Equal(5, assistant.Usage.OutputTokens);
        Assert.Equal(2, assistant.Usage.ReasoningTokens);
        Assert.Equal(7, assistant.Usage.CachedInputTokens);
        Assert.Equal(25, assistant.Usage.TotalTokens);
        Assert.Equal(1, assistant.Usage.CacheWriteInputTokens);
        ChatTurnUsage second = (await reading.Set<ChatEntry>().Include(entry => entry.Usage).SingleAsync(entry => entry.Sequence == 4)).Usage!;
        Assert.Equal(3, second.InputTokens);
        Assert.Equal(4, second.TotalTokens);
        Assert.Null(second.ReasoningTokens);
        Assert.NotEqual(assistant.Usage.EntryId, second.EntryId);
    }

    [Fact]
    public async Task Tool_messages_survive_a_later_turn_that_does_not_load_them()
    {
        await using ChatPersistenceContext context = await CreateContextAsync();
        context.Conversations.Add(ChatConversation.Open(
            "thread-1",
            "user-1",
            "general",
            "سلام",
            "درود",
            null,
            null,
            At,
            toolMessages: ["{\"call\":\"1\"}"]));
        await context.SaveChangesAsync();

        await using ChatPersistenceContext appending = await CreateContextAsync(context.Connection);
        ChatConversation stored = await appending.Conversations.Include(item => item.Entries).SingleAsync();
        stored.AppendTurn("user-1", "general", "ادامه", "باشه", null, null, At.AddMinutes(1));
        await appending.SaveChangesAsync();

        await using ChatPersistenceContext reading = await CreateContextAsync(context.Connection);
        ChatEntry assistant = await reading.Set<ChatEntry>()
            .Include(entry => entry.ToolMessages)
            .SingleAsync(entry => entry.Sequence == 2);
        ChatToolMessage tool = Assert.Single(assistant.ToolMessages);
        Assert.Equal("{\"call\":\"1\"}", tool.Payload);
        Assert.Empty((await reading.Set<ChatEntry>().Include(entry => entry.ToolMessages).SingleAsync(entry => entry.Sequence == 4)).ToolMessages);
    }

    private static async Task<ChatPersistenceContext> CreateContextAsync(SqliteConnection? connection = null)
    {
        bool ownsConnection = connection is null;
        connection ??= new SqliteConnection("Data Source=:memory:");
        if (ownsConnection)
        {
            await connection.OpenAsync();
        }

        DbContextOptions<ChatPersistenceContext> options = new DbContextOptionsBuilder<ChatPersistenceContext>()
            .UseSqlite(connection)
            .Options;
        ChatPersistenceContext context = new(options, connection, ownsConnection);
        if (ownsConnection)
        {
            await context.Database.EnsureCreatedAsync();
        }

        return context;
    }

    private sealed class ChatPersistenceContext(
        DbContextOptions<ChatPersistenceContext> options,
        SqliteConnection connection,
        bool ownsConnection) : DbContext(options)
    {
        public SqliteConnection Connection { get; } = connection;

        public DbSet<ChatConversation> Conversations => Set<ChatConversation>();

        public override void Dispose()
        {
            base.Dispose();
            if (ownsConnection)
            {
                Connection.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (ownsConnection)
            {
                await Connection.DisposeAsync();
            }
        }

        protected override void OnModelCreating(ModelBuilder builder) => builder.ConfigureChatHistory();
    }
}
