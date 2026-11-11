using Maf.Agent.Api.Models.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Maf.Agent.Api.Models.Infrastructure;

public static class ModelProviderConfiguration
{
    public static ModelBuilder ConfigureModelProviders(this ModelBuilder builder)
    {
        builder.Entity<ModelProvider>(entity =>
        {
            entity.ToTable("ModelProviders");
            entity.HasKey(provider => provider.Id);
            entity.Property(provider => provider.Name).HasMaxLength(200);
            entity.Property(provider => provider.NormalizedName).HasMaxLength(200);
            entity.Property(provider => provider.Kind)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(provider => provider.BaseAddress)
                .HasMaxLength(2000)
                .HasConversion(new ValueConverter<Uri, string>(
                    uri => uri.ToString(),
                    value => new Uri(value)));
            entity.Property(provider => provider.ApiKey).HasMaxLength(2000);
            entity.Property(provider => provider.Protocol)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(provider => provider.DiscoveryError).HasMaxLength(2000);
            entity.HasIndex(provider => provider.NormalizedName).IsUnique();
            entity.Navigation(provider => provider.Models)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(provider => provider.Models)
                .WithOne()
                .HasForeignKey(model => model.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ProviderModel>(entity =>
        {
            entity.ToTable("ProviderModels");
            entity.HasKey(model => model.Id);
            entity.Property(model => model.Name).HasMaxLength(200);
            entity.Property(model => model.Capabilities)
                .HasConversion<string>()
                .HasMaxLength(200);
            entity.HasIndex(model => new { model.ProviderId, model.Name }).IsUnique();
        });

        builder.Entity<ModelDefault>(entity =>
        {
            entity.ToTable("ModelDefaults");
            entity.HasKey(item => item.Operation);
            entity.Property(item => item.Operation)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(item => item.ModelName).HasMaxLength(200);
            entity.HasOne<ModelProvider>()
                .WithMany()
                .HasForeignKey(item => item.ProviderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        return builder;
    }
}
