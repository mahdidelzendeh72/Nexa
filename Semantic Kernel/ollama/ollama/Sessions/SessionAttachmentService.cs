using System.Text;
using ollama.Configuration;

namespace ollama.Sessions;

public sealed class SessionAttachmentService(
    IChatSessionStore sessionStore,
    ChatOptions chatOptions) : ISessionAttachmentService
{
    private const int DefaultMaxAttachmentBytes = 5 * 1024 * 1024;
    private const int DefaultMaxTextCharsInPrompt = 32_000;

    public async Task<UploadAttachmentResult> UploadAsync(
        string userId,
        string sessionId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var session = sessionStore.Get(userId, sessionId);
        var maxBytes = chatOptions.MaxAttachmentBytes > 0
            ? chatOptions.MaxAttachmentBytes
            : DefaultMaxAttachmentBytes;

        await using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);

        if (memory.Length == 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        if (memory.Length > maxBytes)
        {
            throw new InvalidOperationException(
                $"File exceeds maximum size of {maxBytes} bytes.");
        }

        var normalizedContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType;

        var attachment = new SessionAttachment
        {
            FileId = Guid.NewGuid().ToString("N")[..8],
            FileName = Path.GetFileName(fileName),
            ContentType = normalizedContentType,
            Content = memory.ToArray()
        };

        session.Attachments[attachment.FileId] = attachment;

        return new UploadAttachmentResult(
            attachment.FileId,
            attachment.FileName,
            attachment.ContentType,
            attachment.Content.Length);
    }

    public IReadOnlyList<SessionAttachmentInfo> List(string userId, string sessionId)
    {
        var session = sessionStore.Get(userId, sessionId);

        return session.Attachments.Values
            .OrderBy(attachment => attachment.UploadedAt)
            .Select(ToInfo)
            .ToList();
    }

    public string ComposeMessageWithAttachments(
        ChatSession session,
        string message,
        IEnumerable<string>? fileIds)
    {
        if (fileIds is null)
        {
            return message;
        }

        var ids = fileIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
        {
            return message;
        }

        var builder = new StringBuilder();
        var maxTextChars = chatOptions.MaxAttachmentTextCharsInPrompt > 0
            ? chatOptions.MaxAttachmentTextCharsInPrompt
            : DefaultMaxTextCharsInPrompt;

        foreach (var fileId in ids)
        {
            if (!session.Attachments.TryGetValue(fileId, out var attachment))
            {
                throw new KeyNotFoundException(
                    $"Attachment '{fileId}' was not found in session '{session.SessionId}'.");
            }

            builder.AppendLine($"--- Attachment: {attachment.FileName} (id: {attachment.FileId}) ---");
            builder.AppendLine($"Content-Type: {attachment.ContentType}");
            builder.AppendLine($"Size: {attachment.Content.Length} bytes");

            if (TryDecodeText(attachment, out var text))
            {
                if (text.Length > maxTextChars)
                {
                    text = text[..maxTextChars] + "\n...[truncated]";
                }

                builder.AppendLine("Content:");
                builder.AppendLine(text);
            }
            else
            {
                builder.AppendLine("Content: [binary file — text content not included in prompt]");
            }

            builder.AppendLine("---");
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            builder.AppendLine();
            builder.Append(message.Trim());
        }

        return builder.ToString().Trim();
    }

    private static SessionAttachmentInfo ToInfo(SessionAttachment attachment) =>
        new(
            attachment.FileId,
            attachment.FileName,
            attachment.ContentType,
            attachment.Content.Length,
            attachment.UploadedAt);

    private static bool TryDecodeText(SessionAttachment attachment, out string text)
    {
        text = string.Empty;

        if (IsLikelyBinary(attachment.Content))
        {
            return false;
        }

        if (!IsTextLikeContentType(attachment.ContentType, attachment.FileName))
        {
            return false;
        }

        text = Encoding.UTF8.GetString(attachment.Content);
        return true;
    }

    private static bool IsTextLikeContentType(string contentType, string fileName)
    {
        if (contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            || contentType.Contains("xml", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var extension = Path.GetExtension(fileName);
        return extension.Equals(".txt", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".sql", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyBinary(ReadOnlySpan<byte> content)
    {
        var sampleLength = Math.Min(content.Length, 8000);
        var nullCount = 0;

        for (var i = 0; i < sampleLength; i++)
        {
            if (content[i] == 0)
            {
                nullCount++;
            }
        }

        return nullCount > 0;
    }
}
