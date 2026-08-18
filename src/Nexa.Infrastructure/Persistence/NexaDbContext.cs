using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nexa.Application.Abstractions;
using Nexa.Domain.Agents;
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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(NexaDbContext).Assembly);
    }
}
