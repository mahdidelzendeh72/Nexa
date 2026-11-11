using Maf.Agent.Api.Chat.Domain;

namespace Maf.Agent.Api.Tests;

public sealed class ChatConversationTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-08T08:00:00Z");

    [Fact]
    public void Opening_a_chat_stores_the_turn_under_the_ag_ui_thread()
    {
        ChatConversation conversation = ChatConversation.Open(
            "abc123",
            "user-1",
            "general",
            "سلام",
            "درود",
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "llama3.2",
            At);

        Assert.Equal("abc123", conversation.ThreadId);
        Assert.Equal("سلام", conversation.Title);
        Assert.Equal(ChatReaction.None, conversation.Entries.Single(entry => entry.Role == ChatSpeaker.Assistant).Reaction);
        Assert.Collection(
            conversation.Entries,
            user => Assert.Equal(ChatSpeaker.User, user.Role),
            assistant => Assert.Equal(ChatSpeaker.Assistant, assistant.Role));
    }

    [Fact]
    public void Continuing_appends_only_the_new_turn_for_the_same_owner()
    {
        ChatConversation conversation = Open();

        conversation.AppendTurn("user-1", "general", "ادامه", "باشه", null, "llama3.2", At.AddMinutes(1));

        Assert.Equal(4, conversation.Entries.Count);
        Assert.Equal(4, conversation.Entries[^1].Sequence);
        Assert.Throws<ArgumentException>(() =>
            conversation.AppendTurn("user-2", "general", "نه", "نه", null, null, At));
        Assert.Throws<ArgumentException>(() =>
            conversation.AppendTurn("user-1", "workflow", "نه", "نه", null, null, At));
    }

    [Fact]
    public void A_file_is_stored_on_the_user_turn_as_base64()
    {
        ChatFile notes = new(@"C:\temp\notes.pdf", "Application/PDF; charset=utf-8", "AQID");

        ChatConversation conversation = ChatConversation.Open(
            "abc123",
            "user-1",
            "general",
            "  ",
            "درود",
            null,
            null,
            At,
            attachments: [notes]);

        ChatEntry user = conversation.Entries.Single(entry => entry.Role == ChatSpeaker.User);
        Assert.Equal("", user.Text);
        Assert.Equal("notes.pdf", conversation.Title);
        ChatAttachment stored = Assert.Single(user.Attachments);
        Assert.Equal("notes.pdf", stored.FileName);
        Assert.Equal("application/pdf", stored.MediaType);
        Assert.Equal("AQID", stored.ContentBase64);
        Assert.Equal(1, stored.Sequence);
        Assert.Empty(conversation.Entries.Single(entry => entry.Role == ChatSpeaker.Assistant).Attachments);
    }

    [Fact]
    public void More_than_four_files_or_an_invalid_payload_is_rejected()
    {
        ChatFile file = new("a.txt", "text/plain", "AQID");
        Assert.Throws<ArgumentException>(() => Open(attachments: [file, file, file, file, file]));
        Assert.Throws<ArgumentException>(() => new ChatFile("a.txt", "text/plain", "!!!!"));
        int maxChars = ((ChatFile.MaxBytes + 2) / 3) * 4;
        Assert.Throws<ArgumentException>(() =>
            new ChatFile("a.bin", "application/octet-stream", new string('A', maxChars + 4)));
        Assert.Throws<ArgumentException>(() => Open(userText: "  "));
    }

    [Fact]
    public void Continuing_keeps_each_turn_s_files_on_that_turn()
    {
        ChatFile first = new("first.txt", "text/plain", "AQID");
        ChatConversation conversation = Open(attachments: [first]);

        conversation.AppendTurn(
            "user-1",
            "general",
            "ادامه",
            "باشه",
            null,
            null,
            At.AddMinutes(1),
            attachments: [new ChatFile("second.png", "image/png", "AQID")]);

        Assert.Equal("first.txt", conversation.Entries.Single(entry => entry.Sequence == 1).Attachments.Single().FileName);
        Assert.Equal("second.png", conversation.Entries.Single(entry => entry.Sequence == 3).Attachments.Single().FileName);
        Assert.Empty(conversation.Entries.Single(entry => entry.Sequence == 4).Attachments);
    }

    [Fact]
    public void Reply_files_belong_to_the_assistant_message()
    {
        ChatFile chart = new("chart.png", "image/png", "AQID");
        ChatFile link = new("link", "text/uri-list", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("https://example.com/a")));

        ChatConversation conversation = ChatConversation.Open(
            "abc123",
            "user-1",
            "general",
            "سلام",
            " ",
            null,
            null,
            At,
            assistantAttachments: [chart, link]);

        ChatEntry assistant = conversation.Entries.Single(entry => entry.Role == ChatSpeaker.Assistant);
        Assert.Equal("", assistant.Text);
        Assert.Equal(["chart.png", "link"], assistant.Attachments.Select(file => file.FileName));
        Assert.Empty(conversation.Entries.Single(entry => entry.Role == ChatSpeaker.User).Attachments);
    }

    [Fact]
    public void Token_counts_belong_to_the_assistant_reply_of_that_turn()
    {
        ChatTurnUsage usage = ChatTurnUsage.Record(12, 4, 1, 3, 16, 2)!;

        ChatConversation conversation = ChatConversation.Open(
            "abc123",
            "user-1",
            "general",
            "سلام",
            "درود",
            null,
            null,
            At,
            usage: usage);

        Assert.Null(conversation.Entries.Single(entry => entry.Role == ChatSpeaker.User).Usage);
        ChatTurnUsage stored = conversation.Entries.Single(entry => entry.Role == ChatSpeaker.Assistant).Usage!;
        Assert.Equal(12, stored.InputTokens);
        Assert.Equal(4, stored.OutputTokens);
        Assert.Equal(1, stored.ReasoningTokens);
        Assert.Equal(3, stored.CachedInputTokens);
        Assert.Equal(16, stored.TotalTokens);
        Assert.Equal(2, stored.CacheWriteInputTokens);

        conversation.AppendTurn("user-1", "general", "ادامه", "باشه", null, null, At.AddMinutes(1));
        Assert.Null(conversation.Entries.Single(entry => entry.Sequence == 4).Usage);
        Assert.Throws<ArgumentOutOfRangeException>(() => ChatTurnUsage.Record(-1, 1, null, null, null));
        Assert.Null(ChatTurnUsage.Record(null, null, null, null, null));
    }

    [Fact]
    public void Reaction_belongs_to_one_assistant_reply()
    {
        ChatConversation conversation = Open();
        conversation.AppendTurn("user-1", "general", "ادامه", "پاسخ دوم", null, null, At.AddMinutes(1));

        Assert.True(conversation.React("user-1", 4, ChatReaction.Dislike, At));
        Assert.Equal(ChatReaction.None, conversation.Entries.Single(entry => entry.Sequence == 2).Reaction);
        Assert.Equal(ChatReaction.Dislike, conversation.Entries.Single(entry => entry.Sequence == 4).Reaction);

        Assert.False(conversation.React("user-1", 9, ChatReaction.Like, At));
        Assert.Throws<ArgumentException>(() => conversation.React("user-1", 1, ChatReaction.Like, At));
        Assert.Throws<ArgumentException>(() => conversation.React("user-2", 4, ChatReaction.Like, At));
    }

    [Fact]
    public void Tool_messages_stay_on_the_assistant_entry()
    {
        ChatConversation conversation = ChatConversation.Open(
            "abc123",
            "user-1",
            "general",
            "سلام",
            "درود",
            null,
            null,
            At,
            toolMessages: ["{\"call\":\"1\"}"]);

        Assert.Equal(2, conversation.Entries.Count);
        Assert.Empty(conversation.Entries.Single(entry => entry.Role == ChatSpeaker.User).ToolMessages);
        ChatToolMessage tool = Assert.Single(conversation.Entries.Single(entry => entry.Role == ChatSpeaker.Assistant).ToolMessages);
        Assert.Equal(1, tool.Sequence);
        Assert.Equal("{\"call\":\"1\"}", tool.Payload);
        Assert.Throws<ArgumentException>(() => ChatConversation.Open(
            "abc123",
            "user-1",
            "general",
            "سلام",
            "درود",
            null,
            null,
            At,
            toolMessages: [new string('x', ChatToolMessage.MaxPayloadLength + 1)]));
    }

    private static ChatConversation Open(string userText = "سلام", IReadOnlyList<ChatFile>? attachments = null) =>
        ChatConversation.Open("abc123", "user-1", "general", userText, "درود", null, null, At, attachments: attachments);
}
