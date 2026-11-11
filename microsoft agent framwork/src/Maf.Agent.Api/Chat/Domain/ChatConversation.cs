using System.Text.RegularExpressions;

namespace Maf.Agent.Api.Chat.Domain;

public enum ChatReaction
{
    None,
    Like,
    Dislike,
}

public enum ChatSpeaker
{
    User,
    Assistant,
}

public sealed class ChatConversation
{
    public const int MaxThreadIdLength = 64;

    public const int MaxTitleLength = 80;

    public const int MaxTextLength = 100_000;

    public const int MaxServiceConversationIdLength = 200;

    private static readonly Regex ThreadIdPattern = new("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant);

    private readonly List<ChatEntry> _entries = [];

    private ChatConversation()
    {
        ThreadId = "";
        OwnerUserId = "";
        Surface = "";
        Title = "";
    }

    private ChatConversation(
        string threadId,
        string ownerUserId,
        string surface,
        string userText,
        string assistantText,
        Guid? providerId,
        string? modelName,
        string? serviceConversationId,
        IReadOnlyList<ChatFile> attachments,
        IReadOnlyList<ChatFile> assistantAttachments,
        ChatTurnUsage? usage,
        IReadOnlyList<string> toolMessages,
        DateTimeOffset createdAt)
    {
        ThreadId = threadId;
        OwnerUserId = ownerUserId;
        Surface = surface;
        Title = TitleFrom(userText, attachments);
        ProviderId = providerId;
        ModelName = modelName;
        ServiceConversationId = NormalizeServiceConversationId(serviceConversationId);
        CreatedAt = createdAt.ToUniversalTime();
        UpdatedAt = CreatedAt;
        AddEntry(ChatSpeaker.User, userText, createdAt, attachments, null, []);
        AddEntry(ChatSpeaker.Assistant, assistantText, createdAt, assistantAttachments, usage, toolMessages);
    }

    public string ThreadId { get; private set; }

    public string OwnerUserId { get; private set; }

    public string Surface { get; private set; }

    public string Title { get; private set; }

    public Guid? ProviderId { get; private set; }

    public string? ModelName { get; private set; }

    public string? ServiceConversationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<ChatEntry> Entries => _entries;

    public static ChatConversation Open(
        string threadId,
        string ownerUserId,
        string surface,
        string userText,
        string assistantText,
        Guid? providerId,
        string? modelName,
        DateTimeOffset createdAt,
        string? serviceConversationId = null,
        IReadOnlyList<ChatFile>? attachments = null,
        IReadOnlyList<ChatFile>? assistantAttachments = null,
        ChatTurnUsage? usage = null,
        IReadOnlyList<string>? toolMessages = null)
    {
        RequireIdentity(threadId, ownerUserId, surface);
        IReadOnlyList<ChatFile> files = NormalizeAttachments(attachments);
        IReadOnlyList<ChatFile> replyFiles = NormalizeAttachments(assistantAttachments);
        return new ChatConversation(
            threadId,
            ownerUserId,
            surface,
            NormalizeUserText(userText, files.Count > 0),
            NormalizeAssistantText(assistantText, replyFiles.Count > 0),
            providerId,
            NormalizeModelName(modelName),
            serviceConversationId,
            files,
            replyFiles,
            usage,
            NormalizeToolMessages(toolMessages),
            createdAt);
    }

    public void AppendTurn(
        string ownerUserId,
        string surface,
        string userText,
        string assistantText,
        Guid? providerId,
        string? modelName,
        DateTimeOffset at,
        string? serviceConversationId = null,
        IReadOnlyList<ChatFile>? attachments = null,
        IReadOnlyList<ChatFile>? assistantAttachments = null,
        ChatTurnUsage? usage = null,
        IReadOnlyList<string>? toolMessages = null)
    {
        if (!string.Equals(OwnerUserId, ownerUserId, StringComparison.Ordinal))
        {
            throw new ArgumentException("این گفتگو مال کاربر دیگری است.");
        }

        if (!string.Equals(Surface, surface, StringComparison.Ordinal))
        {
            throw new ArgumentException("این گفتگو مال سطح دیگری است.");
        }

        IReadOnlyList<ChatFile> files = NormalizeAttachments(attachments);
        IReadOnlyList<ChatFile> replyFiles = NormalizeAttachments(assistantAttachments);
        AddEntry(ChatSpeaker.User, NormalizeUserText(userText, files.Count > 0), at, files, null, []);
        AddEntry(
            ChatSpeaker.Assistant,
            NormalizeAssistantText(assistantText, replyFiles.Count > 0),
            at,
            replyFiles,
            usage,
            NormalizeToolMessages(toolMessages));
        ProviderId = providerId;
        ModelName = NormalizeModelName(modelName);
        if (NormalizeServiceConversationId(serviceConversationId) is string serviceId)
        {
            ServiceConversationId = serviceId;
        }

        UpdatedAt = at.ToUniversalTime();
    }

    public bool React(string ownerUserId, int sequence, ChatReaction reaction, DateTimeOffset at)
    {
        if (!string.Equals(OwnerUserId, ownerUserId, StringComparison.Ordinal))
        {
            throw new ArgumentException("این گفتگو مال کاربر دیگری است.");
        }

        if (!Enum.IsDefined(reaction))
        {
            throw new ArgumentException("بازخورد شناخته نیست.");
        }

        ChatEntry? entry = _entries.FirstOrDefault(item => item.Sequence == sequence);
        if (entry is null)
        {
            return false;
        }

        if (entry.Role != ChatSpeaker.Assistant)
        {
            throw new ArgumentException("بازخورد فقط برای پاسخ دستیار است.");
        }

        entry.React(reaction);
        UpdatedAt = at.ToUniversalTime();
        return true;
    }

    public static bool IsThreadId(string? threadId) =>
        !string.IsNullOrWhiteSpace(threadId) && ThreadIdPattern.IsMatch(threadId);

    public static bool IsSurface(string? surface) =>
        ChatSurface.All.Any(item => string.Equals(item.Name, surface, StringComparison.Ordinal));

    private void AddEntry(
        ChatSpeaker role,
        string text,
        DateTimeOffset at,
        IReadOnlyList<ChatFile> attachments,
        ChatTurnUsage? usage,
        IReadOnlyList<string> toolMessages)
    {
        ChatEntry entry = new(ThreadId, role, text, _entries.Count + 1, at.ToUniversalTime());
        foreach (ChatFile file in attachments)
        {
            entry.Attach(file);
        }

        if (usage is not null)
        {
            entry.RecordUsage(usage);
        }

        if (toolMessages.Count > 0)
        {
            entry.RememberTools(toolMessages);
        }

        _entries.Add(entry);
    }

    private static IReadOnlyList<string> NormalizeToolMessages(IReadOnlyList<string>? toolMessages)
    {
        if (toolMessages is null || toolMessages.Count == 0)
        {
            return [];
        }

        if (toolMessages.Any(payload => payload is null))
        {
            throw new ArgumentException("پیام ابزار مشخص نیست.");
        }

        return toolMessages;
    }

    private static void RequireIdentity(string threadId, string ownerUserId, string surface)
    {
        if (!IsThreadId(threadId))
        {
            throw new ArgumentException("شناسهٔ گفتگو باید ۱ تا ۶۴ نویسه از حروف، رقم، خط تیره یا زیرخط باشد.");
        }

        if (string.IsNullOrWhiteSpace(ownerUserId) || ownerUserId.Trim().Length > 450)
        {
            throw new ArgumentException("کاربر گفتگو مشخص نیست.");
        }

        if (!IsSurface(surface))
        {
            throw new ArgumentException("سطح گفتگو شناخته نیست.");
        }
    }

    private static IReadOnlyList<ChatFile> NormalizeAttachments(IReadOnlyList<ChatFile>? attachments)
    {
        if (attachments is null || attachments.Count == 0)
        {
            return [];
        }

        if (attachments.Count > ChatFile.MaxPerMessage)
        {
            throw new ArgumentException("حداکثر ۴ فایل در هر پیام.");
        }

        if (attachments.Any(file => file is null))
        {
            throw new ArgumentException("فایل گفتگو مشخص نیست.");
        }

        return attachments;
    }

    private static string NormalizeUserText(string text, bool hasFiles)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (!hasFiles)
            {
                throw new ArgumentException("متن گفتگو خالی است.");
            }

            return "";
        }

        string trimmed = text.Trim();
        if (trimmed.Length > MaxTextLength)
        {
            throw new ArgumentException("متن گفتگو بیش از حد بلند است.");
        }

        return trimmed;
    }

    private static string NormalizeAssistantText(string text, bool hasFiles)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            if (!hasFiles)
            {
                throw new ArgumentException("متن گفتگو خالی است.");
            }

            return "";
        }

        return RequireText(text);
    }

    private static string RequireText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("متن گفتگو خالی است.");
        }

        string trimmed = text.Trim();
        if (trimmed.Length > MaxTextLength)
        {
            throw new ArgumentException("متن گفتگو بیش از حد بلند است.");
        }

        return trimmed;
    }

    private static string? NormalizeServiceConversationId(string? serviceConversationId)
    {
        if (string.IsNullOrWhiteSpace(serviceConversationId))
        {
            return null;
        }

        string trimmed = serviceConversationId.Trim();
        return trimmed.Length <= MaxServiceConversationIdLength
            ? trimmed
            : trimmed[..MaxServiceConversationIdLength];
    }

    private static string? NormalizeModelName(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
        {
            return null;
        }

        string trimmed = modelName.Trim();
        return trimmed.Length <= 200 ? trimmed : trimmed[..200];
    }

    private static string TitleFrom(string userText, IReadOnlyList<ChatFile> attachments)
    {
        string singleLine = string.IsNullOrWhiteSpace(userText)
            ? attachments[0].FileName
            : userText.ReplaceLineEndings(" ").Trim();
        return singleLine.Length <= MaxTitleLength ? singleLine : singleLine[..MaxTitleLength];
    }
}
