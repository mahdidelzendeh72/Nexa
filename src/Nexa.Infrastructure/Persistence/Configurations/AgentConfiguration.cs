using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexa.Domain.Agents;

namespace Nexa.Infrastructure.Persistence.Configurations;

internal sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("agents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasIndex(x => x.OwnerUserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasMany(x => x.Versions)
            .WithOne()
            .HasForeignKey(x => x.AgentId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class AgentVersionConfiguration : IEntityTypeConfiguration<AgentVersion>
{
    public void Configure(EntityTypeBuilder<AgentVersion> builder)
    {
        builder.ToTable("agent_versions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Instructions).HasMaxLength(32_000).IsRequired();
        builder.HasIndex(x => new { x.AgentId, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => x.ModelProfileId);
        builder.HasOne<Nexa.Domain.Models.ModelProfile>()
            .WithMany()
            .HasForeignKey(x => x.ModelProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
