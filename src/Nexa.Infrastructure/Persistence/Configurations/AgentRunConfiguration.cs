using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexa.Domain.AgentRuntime;
using Nexa.Domain.Conversations;

namespace Nexa.Infrastructure.Persistence.Configurations;

internal sealed class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.ToTable("agent_runs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.CorrelationId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ErrorCode).HasMaxLength(80);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.HasIndex(x => x.ConversationId);
        builder.HasIndex(x => x.AgentVersionId);
        builder.HasIndex(x => x.StartedAt);
        builder.HasOne<Conversation>()
            .WithMany()
            .HasForeignKey(x => x.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Nexa.Domain.Agents.AgentVersion>()
            .WithMany()
            .HasForeignKey(x => x.AgentVersionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.ToolExecutions)
            .WithOne()
            .HasForeignKey(x => x.AgentRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.ToolExecutions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class ToolExecutionConfiguration : IEntityTypeConfiguration<ToolExecution>
{
    public void Configure(EntityTypeBuilder<ToolExecution> builder)
    {
        builder.ToTable("tool_executions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ToolName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.CallId).HasMaxLength(200);
        builder.Property(x => x.Arguments).HasColumnType("text");
        builder.Property(x => x.Result).HasColumnType("text");
        builder.HasIndex(x => x.AgentRunId);
        builder.HasIndex(x => x.StartedAt);
    }
}
