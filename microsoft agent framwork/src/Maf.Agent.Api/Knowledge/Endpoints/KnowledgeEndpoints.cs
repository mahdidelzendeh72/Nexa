using System.Security.Claims;
using Maf.Agent.Api.Data;
using Maf.Agent.Api.Knowledge.Application;
using Maf.Agent.Api.Knowledge.Domain;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Maf.Agent.Api.Knowledge.Endpoints;

public static class KnowledgeEndpoints
{
    public const long MaxUploadBytes = 100L * 1024L * 1024L;

    private static readonly Dictionary<string, string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "application/pdf",
            [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            [".md"] = "text/markdown",
            [".markdown"] = "text/markdown",
            [".txt"] = "text/plain",
        };

    public static IServiceCollection AddKnowledgeApi(
        this IServiceCollection services)
    {
        services.Configure<FormOptions>(
            options => options.MultipartBodyLengthLimit = MaxUploadBytes);
        services.AddAuthorizationBuilder()
            .AddPolicy(
                KnowledgePolicies.Read,
                policy => policy.RequireRole(
                    KnowledgeRoles.Reader,
                    KnowledgeRoles.Manager))
            .AddPolicy(
                KnowledgePolicies.Manage,
                policy => policy.RequireRole(KnowledgeRoles.Manager));
        return services;
    }

    public static IEndpointRouteBuilder MapKnowledgeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/knowledge")
            .WithTags("Knowledge");

        group.MapPost("/documents", UploadAsync)
            .RequireAuthorization(KnowledgePolicies.Manage)
            .DisableAntiforgery();
        group.MapGet("/documents", ListAsync)
            .RequireAuthorization(KnowledgePolicies.Read);
        group.MapGet("/documents/{documentId:guid}", GetAsync)
            .RequireAuthorization(KnowledgePolicies.Read);
        group.MapGet("/documents/{documentId:guid}/download", DownloadAsync)
            .RequireAuthorization(KnowledgePolicies.Read);
        group.MapPost("/documents/{documentId:guid}/retry", RetryAsync)
            .RequireAuthorization(KnowledgePolicies.Manage);
        group.MapPost("/documents/{documentId:guid}/reindex", RetryAsync)
            .RequireAuthorization(KnowledgePolicies.Manage);
        group.MapDelete("/documents/{documentId:guid}", DeleteAsync)
            .RequireAuthorization(KnowledgePolicies.Manage);
        group.MapPost("/search", SearchAsync)
            .RequireAuthorization(KnowledgePolicies.Read);
        group.MapGet("/health", HealthAsync)
            .RequireAuthorization(KnowledgePolicies.Read);

        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        HttpRequest request,
        ClaimsPrincipal user,
        IKnowledgeDocumentStorage storage,
        IKnowledgeIngestionQueue queue,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "A multipart form file is required." });
        }

        IFormCollection form = await request.ReadFormAsync(cancellationToken);
        IFormFile? file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "The uploaded file is empty." });
        }

        if (file.Length > MaxUploadBytes)
        {
            return Results.BadRequest(new { error = "The maximum file size is 100 MB." });
        }

        string fileName = Path.GetFileName(file.FileName);
        string extension = Path.GetExtension(fileName);
        if (!SupportedExtensions.TryGetValue(extension, out string? contentType))
        {
            return Results.BadRequest(new
            {
                error = "Supported formats are PDF, DOCX, XLSX, Markdown, and TXT.",
            });
        }

        string? userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Results.Unauthorized();
        }

        await using Stream stream = file.OpenReadStream();
        if (!await HasExpectedSignatureAsync(extension, stream, cancellationToken))
        {
            return Results.BadRequest(new
            {
                error = "The file content does not match its extension.",
            });
        }

        KnowledgeUploadResult result = await storage.SaveAsync(
            new KnowledgeUpload(
                fileName,
                contentType,
                file.Length,
                userId,
                stream),
            cancellationToken);

        if (!result.AlreadyExisted)
        {
            await queue.EnqueueAsync(result.DocumentId, cancellationToken);
        }

        return result.AlreadyExisted
            ? Results.Ok(new
            {
                documentId = result.DocumentId,
                alreadyExisted = true,
            })
            : Results.Created(
                $"/api/knowledge/documents/{result.DocumentId}",
                new
                {
                    documentId = result.DocumentId,
                    alreadyExisted = false,
                });
    }

    private static async Task<IResult> ListAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        KnowledgeDocument[] documents = await dbContext.KnowledgeDocuments
            .AsNoTracking()
            .OrderByDescending(document => document.CreatedAt)
            .ToArrayAsync(cancellationToken);
        return Results.Ok(documents.Select(ToDto));
    }

    private static async Task<IResult> GetAsync(
        Guid documentId,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        KnowledgeDocument? document = await dbContext.KnowledgeDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == documentId, cancellationToken);
        return document is null
            ? Results.NotFound()
            : Results.Ok(ToDto(document));
    }

    private static async Task<IResult> DownloadAsync(
        Guid documentId,
        HttpContext httpContext,
        IKnowledgeDocumentStorage storage,
        CancellationToken cancellationToken)
    {
        KnowledgeDocumentStream? document =
            await storage.OpenReadAsync(documentId, cancellationToken);
        if (document is null)
        {
            return Results.NotFound();
        }

        httpContext.Response.RegisterForDisposeAsync(document);
        return Results.Stream(
            document.Content,
            document.ContentType,
            document.FileName,
            enableRangeProcessing: false);
    }

    private static async Task<IResult> RetryAsync(
        Guid documentId,
        ApplicationDbContext dbContext,
        IKnowledgeIngestionQueue queue,
        CancellationToken cancellationToken)
    {
        KnowledgeDocument? document =
            await dbContext.KnowledgeDocuments.SingleOrDefaultAsync(
                item => item.Id == documentId,
                cancellationToken);
        if (document is null)
        {
            return Results.NotFound();
        }

        if (document.Status == KnowledgeDocumentStatus.Processing)
        {
            return Results.Conflict(new
            {
                error = "The document is already being processed.",
            });
        }

        document.ScheduleRetry();
        await dbContext.SaveChangesAsync(cancellationToken);
        await queue.EnqueueAsync(documentId, cancellationToken);
        return Results.Accepted($"/api/knowledge/documents/{documentId}");
    }

    private static async Task<IResult> DeleteAsync(
        Guid documentId,
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken)
    {
        int deleted = await dbContext.KnowledgeDocuments
            .Where(document => document.Id == documentId)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 0 ? Results.NotFound() : Results.NoContent();
    }

    private static async Task<IResult> SearchAsync(
        KnowledgeSearchRequest request,
        IKnowledgeRetriever retriever,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return Results.BadRequest(new { error = "A search query is required." });
        }

        KnowledgeSearchResponse response = await retriever.SearchAsync(
            request.Query,
            request.Limit ?? 8,
            cancellationToken);
        return Results.Ok(response);
    }

    private static async Task<IResult> HealthAsync(
        NpgsqlDataSource dataSource,
        IKnowledgeEmbeddingService embeddingService,
        CancellationToken cancellationToken)
    {
        // PostgreSQL extension metadata is not part of the application EF model;
        // this parameterized, read-only catalog query is the intentional exception.
        await using NpgsqlCommand command = dataSource.CreateCommand(
            """
            SELECT extname
            FROM pg_extension
            WHERE extname IN ('vector', 'pg_trgm')
            ORDER BY extname;
            """);
        List<string> extensions = [];
        await using NpgsqlDataReader reader =
            await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            extensions.Add(reader.GetString(0));
        }

        KnowledgeEmbeddingHealth embedding =
            await embeddingService.GetHealthAsync(cancellationToken);
        return Results.Ok(new
        {
            database = "Healthy",
            extensions,
            embedding,
        });
    }

    private static KnowledgeDocumentDto ToDto(KnowledgeDocument document) =>
        new(
            document.Id,
            document.FileName,
            document.ContentType,
            document.Size,
            document.Status.ToString(),
            document.CreatedAt,
            document.CompletedAt,
            document.Error,
            document.ChunkCount,
            document.EmbeddingModel);

    private static async Task<bool> HasExpectedSignatureAsync(
        string extension,
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] header = new byte[8];
        int read = await stream.ReadAsync(header, cancellationToken);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => read >= 4 && header.AsSpan(0, 4).SequenceEqual("%PDF"u8),
            ".docx" or ".xlsx" =>
                read >= 2 && header[0] == (byte)'P' && header[1] == (byte)'K',
            ".md" or ".markdown" or ".txt" => true,
            _ => false,
        };
    }

    private sealed record KnowledgeSearchRequest(string Query, int? Limit);

    private sealed record KnowledgeDocumentDto(
        Guid Id,
        string FileName,
        string ContentType,
        long Size,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt,
        string? Error,
        int ChunkCount,
        string? EmbeddingModel);
}
