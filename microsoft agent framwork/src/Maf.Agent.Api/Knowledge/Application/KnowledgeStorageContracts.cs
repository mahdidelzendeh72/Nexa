namespace Maf.Agent.Api.Knowledge.Application;

public sealed record KnowledgeUpload(
    string FileName,
    string ContentType,
    long Size,
    string CreatedByUserId,
    Stream Content);

public sealed record KnowledgeUploadResult(Guid DocumentId, bool AlreadyExisted);

public sealed class KnowledgeDocumentStream(
    Guid documentId,
    string fileName,
    string contentType,
    long size,
    Stream content,
    IAsyncDisposable owner)
    : IAsyncDisposable
{
    public Guid DocumentId { get; } = documentId;
    public string FileName { get; } = fileName;
    public string ContentType { get; } = contentType;
    public long Size { get; } = size;
    public Stream Content { get; } = content;

    public async ValueTask DisposeAsync()
    {
        await Content.DisposeAsync();
        await owner.DisposeAsync();
    }
}

public interface IKnowledgeDocumentStorage
{
    Task<KnowledgeUploadResult> SaveAsync(
        KnowledgeUpload upload,
        CancellationToken cancellationToken);

    Task<KnowledgeDocumentStream?> OpenReadAsync(
        Guid documentId,
        CancellationToken cancellationToken);
}
