using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nexa.Domain.Models;

namespace Nexa.Infrastructure.Persistence.Configurations;

internal sealed class ModelProviderConfiguration : IEntityTypeConfiguration<ModelProvider>
{
    public void Configure(EntityTypeBuilder<ModelProvider> builder)
    {
        builder.ToTable("model_providers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(64);
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

internal sealed class ModelProfileConfiguration : IEntityTypeConfiguration<ModelProfile>
{
    public void Configure(EntityTypeBuilder<ModelProfile> builder)
    {
        builder.ToTable("model_profiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ModelId).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.ProviderId);
        builder.HasOne<ModelProvider>()
            .WithMany()
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
