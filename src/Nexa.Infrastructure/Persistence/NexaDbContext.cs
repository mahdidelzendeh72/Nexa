using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nexa.Application.Abstractions;
using Nexa.Domain.Agents;
using Nexa.Domain.AgentRuntime;
using Nexa.Domain.Conversations;
using Nexa.Domain.Models;
using Nexa.Infrastructure.Identity;

namespace Nexa.Infrastructure.Persistence;

public sealed class NexaDbContext(DbContextOptions<NexaDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options), IUnitOfWork
{
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<AgentVersion> AgentVersions => Set<AgentVersion>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<ModelProvider> ModelProviders => Set<ModelProvider>();
    public DbSet<ModelProfile> ModelProfiles => Set<ModelProfile>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<ToolExecution> ToolExecutions => Set<ToolExecution>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(NexaDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        MarkNewCollectionChildrenAsAdded();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        MarkNewCollectionChildrenAsAdded();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void MarkNewCollectionChildrenAsAdded()
    {
        foreach (var entry in ChangeTracker.Entries<ConversationMessage>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.State = EntityState.Added;
            }
        }

        foreach (var entry in ChangeTracker.Entries<ToolExecution>())
        {
            if (entry.State == EntityState.Modified && entry.GetDatabaseValues() is null)
            {
                entry.State = EntityState.Added;
            }
        }
    }
}
