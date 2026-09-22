namespace ollama.Sessions;

public sealed class SessionAttachment
{
    public required string FileId { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required byte[] Content { get; init; }

    public DateTime UploadedAt { get; init; } = DateTime.UtcNow;
}

public sealed record SessionAttachmentInfo(
    string FileId,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAt);

public sealed record UploadAttachmentResult(
    string FileId,
    string FileName,
    string ContentType,
    long SizeBytes);
