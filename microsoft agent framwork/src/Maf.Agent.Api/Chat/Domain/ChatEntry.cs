namespace Maf.Agent.Api.Chat.Domain;

public sealed class ChatEntry
{
    private readonly List<ChatAttachment> _attachments = [];

    private readonly List<ChatToolMessage> _toolMessages = [];

    private ChatEntry()
    {
        ThreadId = "";
        Text = "";
    }

    internal ChatEntry(string threadId, ChatSpeaker role, string text, int sequence, DateTimeOffset createdAt)
    {
        ThreadId = threadId;
        Role = role;
        Text = text;
        Sequence = sequence;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public string ThreadId { get; private set; }

    public ChatSpeaker Role { get; private set; }

    public string Text { get; private set; }

    public int Sequence { get; private set; }

    public ChatReaction Reaction { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<ChatAttachment> Attachments => _attachments;

    public IReadOnlyList<ChatToolMessage> ToolMessages => _toolMessages;

    public ChatTurnUsage? Usage { get; private set; }

    internal void Attach(ChatFile file) =>
        _attachments.Add(new ChatAttachment(file, _attachments.Count + 1));

    internal void RememberTools(IReadOnlyList<string> payloads)
    {
        if (Role != ChatSpeaker.Assistant)
        {
            throw new ArgumentException("پیام ابزار فقط برای پاسخ دستیار است.");
        }

        foreach (string payload in payloads)
        {
            _toolMessages.Add(new ChatToolMessage(payload, _toolMessages.Count + 1));
        }
    }

    internal void RecordUsage(ChatTurnUsage usage)
    {
        if (Role != ChatSpeaker.Assistant)
        {
            throw new ArgumentException("مصرف توکن فقط برای پاسخ دستیار است.");
        }

        Usage = usage;
    }

    internal void React(ChatReaction reaction) => Reaction = reaction;
}
