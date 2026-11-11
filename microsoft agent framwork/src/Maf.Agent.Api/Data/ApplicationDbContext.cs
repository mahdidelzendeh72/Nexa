using Maf.Agent.Api.Chat.Domain;
using Maf.Agent.Api.Chat.Infrastructure.History;
using Maf.Agent.Api.Identity.Domain;
using Maf.Agent.Api.Knowledge.Domain;
using Maf.Agent.Api.Knowledge.Infrastructure;
using Maf.Agent.Api.Mcp.Domain;
using Maf.Agent.Api.Mcp.Infrastructure;
using Maf.Agent.Api.Models.Domain;
using Maf.Agent.Api.Models.Infrastructure;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Maf.Agent.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeDocumentContent> KnowledgeDocumentContents => Set<KnowledgeDocumentContent>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<KnowledgeEmbedding> KnowledgeEmbeddings => Set<KnowledgeEmbedding>();
    public DbSet<KnowledgeProcessingAttempt> KnowledgeProcessingAttempts =>
        Set<KnowledgeProcessingAttempt>();

    public DbSet<McpServer> McpServers => Set<McpServer>();
    public DbSet<McpDiscoveredTool> McpDiscoveredTools => Set<McpDiscoveredTool>();
    public DbSet<McpToolApproval> McpToolApprovals => Set<McpToolApproval>();
    public DbSet<ModelProvider> ModelProviders => Set<ModelProvider>();
    public DbSet<ProviderModel> ProviderModels => Set<ProviderModel>();
    public DbSet<ModelDefault> ModelDefaults => Set<ModelDefault>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatEntry> ChatEntries => Set<ChatEntry>();
    public DbSet<ChatAttachment> ChatAttachments => Set<ChatAttachment>();
    public DbSet<ChatTurnUsage> ChatTurnUsages => Set<ChatTurnUsage>();
    public DbSet<ChatToolMessage> ChatToolMessages => Set<ChatToolMessage>();
    public DbSet<ChatTraceThread> ChatTraceThreads => Set<ChatTraceThread>();
    public DbSet<ChatTraceRun> ChatTraceRuns => Set<ChatTraceRun>();
    public DbSet<ChatTraceStep> ChatTraceSteps => Set<ChatTraceStep>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureKnowledgeModel();
        builder.ConfigureMcpModel();
        builder.ConfigureModelProviders();
        builder.ConfigureChatHistory();
        builder.ConfigureChatTrace();
    }
}
