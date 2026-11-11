using Maf.Agent.Api.Knowledge.Domain;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Knowledge.Infrastructure;

public static class KnowledgeModelConfiguration
{
    public static ModelBuilder ConfigureKnowledgeModel(this ModelBuilder builder)
    {
        builder.HasPostgresExtension("pg_trgm");
        builder.HasPostgresExtension("vector");

        builder.Entity<KnowledgeDocument>(entity =>
        {
            entity.ToTable("KnowledgeDocuments");
            entity.HasKey(document => document.Id);
            entity.Property(document => document.FileName).HasMaxLength(512);
            entity.Property(document => document.ContentType).HasMaxLength(256);
            entity.Property(document => document.Sha256).HasMaxLength(64);
            entity.Property(document => document.CreatedByUserId).HasMaxLength(450);
            entity.Property(document => document.EmbeddingModel).HasMaxLength(256);
            entity.Property(document => document.Status)
                .HasConversion<string>()
                .HasMaxLength(64);
            entity.HasIndex(document => document.Sha256).IsUnique();
            entity.HasIndex(document => document.Status);
        });

        builder.Entity<KnowledgeDocumentContent>(entity =>
        {
            entity.ToTable("KnowledgeDocumentContents");
            entity.HasKey(content => content.DocumentId);
            entity.Property(content => content.Data).HasColumnType("bytea");
            entity
                .HasOne(content => content.Document)
                .WithOne(document => document.OriginalContent)
                .HasForeignKey<KnowledgeDocumentContent>(content => content.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<KnowledgeChunk>(entity =>
        {
            entity.ToTable("KnowledgeChunks");
            entity.HasKey(chunk => chunk.Id);
            entity.Property(chunk => chunk.SheetName).HasMaxLength(256);
            entity.Property(chunk => chunk.Section).HasMaxLength(512);
            entity
                .HasGeneratedTsVectorColumn(
                    chunk => chunk.SearchVector,
                    "simple",
                    chunk => new { chunk.NormalizedContent })
                .HasIndex(chunk => chunk.SearchVector)
                .HasMethod("GIN");
            entity
                .HasIndex(chunk => chunk.NormalizedContent)
                .HasMethod("GIN")
                .HasOperators("gin_trgm_ops");
            entity.HasIndex(chunk => new { chunk.DocumentId, chunk.Ordinal })
                .IsUnique();
            entity
                .HasOne(chunk => chunk.Document)
                .WithMany(document => document.Chunks)
                .HasForeignKey(chunk => chunk.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<KnowledgeEmbedding>(entity =>
        {
            entity.ToTable("KnowledgeEmbeddings");
            entity.HasKey(embedding => embedding.ChunkId);
            entity.Property(embedding => embedding.Model).HasMaxLength(256);
            entity.Property(embedding => embedding.Value).HasColumnType("vector(1024)");
            entity
                .HasIndex(embedding => embedding.Value)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
            entity
                .HasOne(embedding => embedding.Chunk)
                .WithOne(chunk => chunk.Embedding)
                .HasForeignKey<KnowledgeEmbedding>(embedding => embedding.ChunkId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<KnowledgeProcessingAttempt>(entity =>
        {
            entity.ToTable("KnowledgeProcessingAttempts");
            entity.HasKey(attempt => attempt.Id);
            entity.Property(attempt => attempt.Stage)
                .HasConversion<string>()
                .HasMaxLength(64);
            entity.HasIndex(attempt => new
                {
                    attempt.DocumentId,
                    attempt.AttemptNumber,
                })
                .IsUnique();
            entity
                .HasOne(attempt => attempt.Document)
                .WithMany(document => document.Attempts)
                .HasForeignKey(attempt => attempt.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        return builder;
    }
}
