using Maf.Agent.Api.Mcp.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Maf.Agent.Api.Mcp.Infrastructure;

public static class McpModelConfiguration
{
    public static ModelBuilder ConfigureMcpModel(this ModelBuilder builder)
    {
        builder.Entity<McpServer>(entity =>
        {
            entity.ToTable("McpServers");
            entity.HasKey(server => server.Id);
            entity.Property(server => server.Name).HasMaxLength(200);
            entity.Property(server => server.NormalizedName).HasMaxLength(200);
            entity.Property(server => server.Description).HasMaxLength(2000);
            entity.Property(server => server.Transport)
                .HasConversion<string>()
                .HasMaxLength(32);
            entity.Property(server => server.Endpoint)
                .HasMaxLength(2000)
                .HasConversion(new ValueConverter<Uri?, string?>(
                    uri => uri == null ? null : uri.ToString(),
                    value => value == null ? null : new Uri(value)));
            entity.Property(server => server.HeadersJson).HasMaxLength(8000);
            entity.Property(server => server.Command).HasMaxLength(260);
            entity.Property(server => server.ArgumentsJson).HasMaxLength(8000);
            entity.Property(server => server.EnvironmentJson).HasMaxLength(8000);
            entity.Property(server => server.DiscoveryError).HasMaxLength(2000);
            entity.HasIndex(server => server.NormalizedName).IsUnique();
            entity.HasIndex(server => server.IsEnabled);
            entity.Navigation(server => server.Tools)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(server => server.Tools)
                .WithOne()
                .HasForeignKey(tool => tool.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(server => server.ToolApprovals)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.HasMany(server => server.ToolApprovals)
                .WithOne()
                .HasForeignKey(approval => approval.ServerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<McpDiscoveredTool>(entity =>
        {
            entity.ToTable("McpDiscoveredTools");
            entity.HasKey(tool => tool.Id);
            entity.Property(tool => tool.Name).HasMaxLength(256);
            entity.Property(tool => tool.Title).HasMaxLength(512);
            entity.HasIndex(tool => new { tool.ServerId, tool.Name }).IsUnique();
        });

        builder.Entity<McpToolApproval>(entity =>
        {
            entity.ToTable("McpToolApprovals");
            entity.HasKey(approval => approval.Id);
            entity.Property(approval => approval.Name).HasMaxLength(256);
            entity.HasIndex(approval => new { approval.ServerId, approval.Name }).IsUnique();
        });

        return builder;
    }
}
