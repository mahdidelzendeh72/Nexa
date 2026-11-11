using System.Data;
using System.Security.Cryptography;
using Maf.Agent.Api.Data;
using Maf.Agent.Api.Knowledge.Application;
using Maf.Agent.Api.Knowledge.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace Maf.Agent.Api.Knowledge.Infrastructure;

public sealed class PostgresKnowledgeDocumentStorage(
    ApplicationDbContext dbContext,
    NpgsqlDataSource dataSource)
    : IKnowledgeDocumentStorage
{
    public async Task<KnowledgeUploadResult> SaveAsync(
        KnowledgeUpload upload,
        CancellationToken cancellationToken)
    {
        await using PreparedStream prepared =
            await PrepareSeekableStreamAsync(upload.Content, cancellationToken);

        string sha256 = await ComputeSha256Async(prepared.Stream, cancellationToken);
        prepared.Stream.Position = 0;

        Guid? existingId = await FindByHashAsync(sha256, cancellationToken);
        if (existingId is not null)
        {
            return new KnowledgeUploadResult(existingId.Value, true);
        }

        KnowledgeDocument document = KnowledgeDocument.Create(
            Guid.NewGuid(),
            upload.FileName,
            upload.ContentType,
            upload.Size,
            sha256,
            upload.CreatedByUserId,
            DateTimeOffset.UtcNow);
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.KnowledgeDocuments.Add(document);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            })
        {
            await transaction.RollbackAsync(cancellationToken);
            await transaction.DisposeAsync();
            dbContext.ChangeTracker.Clear();
            Guid duplicateId = await FindByHashAsync(sha256, cancellationToken)
                ?? throw new InvalidOperationException(
                    "A duplicate document was detected but its record could not be loaded.",
                    exception);
            return new KnowledgeUploadResult(duplicateId, true);
        }

        NpgsqlConnection connection =
            (NpgsqlConnection)dbContext.Database.GetDbConnection();
        NpgsqlTransaction npgsqlTransaction =
            (NpgsqlTransaction)transaction.GetDbTransaction();

        // EF Core materializes bytea as byte[]. Npgsql is used only here so the
        // original file can be streamed without buffering up to 100 MB in memory.
        await using NpgsqlCommand insertContent = new(
            """
            INSERT INTO "KnowledgeDocumentContents" ("DocumentId", "Data")
            VALUES (@documentId, @data);
            """,
            connection,
            npgsqlTransaction);
        insertContent.Parameters.AddWithValue("documentId", document.Id);
        NpgsqlParameter dataParameter =
            insertContent.Parameters.Add("data", NpgsqlDbType.Bytea);
        dataParameter.Value = prepared.Stream;
        dataParameter.Size = checked((int)upload.Size);

        await insertContent.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new KnowledgeUploadResult(document.Id, false);
    }

    public async Task<KnowledgeDocumentStream?> OpenReadAsync(
        Guid documentId,
        CancellationToken cancellationToken)
    {
        StoredDocumentMetadata? metadata = await dbContext.KnowledgeDocuments
            .AsNoTracking()
            .Where(document => document.Id == documentId)
            .Select(document => new StoredDocumentMetadata(
                document.Id,
                document.FileName,
                document.ContentType,
                document.Size))
            .SingleOrDefaultAsync(cancellationToken);
        if (metadata is null)
        {
            return null;
        }

        NpgsqlConnection connection =
            await dataSource.OpenConnectionAsync(cancellationToken);
        // SequentialAccess over a raw Npgsql reader is intentional: EF Core
        // cannot expose a bytea column as a streaming response.
        NpgsqlCommand command = new(
            """
            SELECT "Data"
            FROM "KnowledgeDocumentContents"
            WHERE "DocumentId" = @documentId;
            """,
            connection);
        command.Parameters.AddWithValue("documentId", documentId);

        NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            CommandBehavior.SequentialAccess | CommandBehavior.SingleRow,
            cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            await reader.DisposeAsync();
            await command.DisposeAsync();
            await connection.DisposeAsync();
            return null;
        }

        Stream content = await reader.GetStreamAsync(0, cancellationToken);

        ReaderOwner owner = new(reader, command, connection);
        return new KnowledgeDocumentStream(
            metadata.Id,
            metadata.FileName,
            metadata.ContentType,
            metadata.Size,
            content,
            owner);
    }

    private async Task<Guid?> FindByHashAsync(
        string sha256,
        CancellationToken cancellationToken) =>
        await dbContext.KnowledgeDocuments
            .AsNoTracking()
            .Where(document => document.Sha256 == sha256)
            .Select(document => (Guid?)document.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private static async Task<string> ComputeSha256Async(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static async Task<PreparedStream> PrepareSeekableStreamAsync(
        Stream source,
        CancellationToken cancellationToken)
    {
        if (source.CanSeek)
        {
            source.Position = 0;
            return new PreparedStream(source, ownsStream: false);
        }

        string tempFile = Path.GetTempFileName();
        FileStream stream = new(
            tempFile,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await source.CopyToAsync(stream, cancellationToken);
        stream.Position = 0;
        return new PreparedStream(stream, ownsStream: true);
    }

    private sealed class PreparedStream(Stream stream, bool ownsStream)
        : IAsyncDisposable
    {
        public Stream Stream { get; } = stream;

        public async ValueTask DisposeAsync()
        {
            if (ownsStream)
            {
                await Stream.DisposeAsync();
            }
        }
    }

    private sealed record StoredDocumentMetadata(
        Guid Id,
        string FileName,
        string ContentType,
        long Size);

    private sealed class ReaderOwner(
        NpgsqlDataReader reader,
        NpgsqlCommand command,
        NpgsqlConnection connection)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await reader.DisposeAsync();
            await command.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
