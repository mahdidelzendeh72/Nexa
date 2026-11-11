using Maf.Agent.Api.Chat.Domain;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Chat.Infrastructure.History;

public static class ChatTraceConfiguration
{
    public static ModelBuilder ConfigureChatTrace(this ModelBuilder builder)
    {
        builder.Entity<ChatTraceThread>(entity =>
        {
            entity.ToTable("ChatTraceThreads");
            entity.HasKey(thread => thread.ThreadId);
            entity.Property(thread => thread.ThreadId).HasMaxLength(ChatConversation.MaxThreadIdLength);
            entity.Property(thread => thread.UserId).HasMaxLength(450);
            entity.Property(thread => thread.LastOutcome).HasMaxLength(32);
            entity.HasIndex(thread => new { thread.UserId, thread.LastRunAt });
        });

        builder.Entity<ChatTraceRun>(entity =>
        {
            entity.ToTable("ChatTraceRuns");
            entity.HasKey(run => run.RunId);
            entity.Property(run => run.RunId).HasMaxLength(80);
            entity.Property(run => run.ThreadId).HasMaxLength(ChatConversation.MaxThreadIdLength);
            entity.Property(run => run.Outcome).HasMaxLength(32);
            entity.Property(run => run.Model).HasMaxLength(200);
            entity.HasIndex(run => new { run.ThreadId, run.StartedAt });
            entity.Navigation(run => run.Steps).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(run => run.Steps)
                .WithOne()
                .HasForeignKey(step => step.RunId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<ChatTraceThread>()
                .WithMany()
                .HasForeignKey(run => run.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatTraceStep>(entity =>
        {
            entity.ToTable("ChatTraceSteps");
            entity.HasKey(step => step.Id);
            entity.Property(step => step.Kind).HasMaxLength(32);
            entity.Property(step => step.RefId).HasMaxLength(80);
            entity.Property(step => step.Name).HasMaxLength(200);
            entity.HasIndex(step => new { step.RunId, step.Sequence }).IsUnique();
        });

        return builder;
    }
}
