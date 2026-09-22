namespace ollama.Sessions;

public interface ISessionAttachmentService
{
    Task<UploadAttachmentResult> UploadAsync(
        string userId,
        string sessionId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken cancellationToken = default);

    IReadOnlyList<SessionAttachmentInfo> List(string userId, string sessionId);

    string ComposeMessageWithAttachments(
        ChatSession session,
        string message,
        IEnumerable<string>? fileIds);
}
